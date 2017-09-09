﻿using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ShaderGen
{
    public class HlslMethodVisitor : CSharpSyntaxVisitor<string>
    {
        private readonly SemanticModel _model;
        private readonly LanguageBackend _backend;
        private readonly ShaderFunction _shaderFunction;
        public string _value;

        public HlslMethodVisitor(SemanticModel model, ShaderFunction shaderFunction)
        {
            _model = model;
            _shaderFunction = shaderFunction;
            _backend = new HlslBackend(model);
        }

        public override string VisitBlock(BlockSyntax node)
        {
            StringBuilder sb = new StringBuilder();
            string returnType = _backend.CSharpToShaderType(_shaderFunction.ReturnType.Name);
            sb.AppendLine($"{returnType} {_shaderFunction.Name}({GetParameterDeclList()})");
            sb.AppendLine("{");

            foreach (StatementSyntax ss in node.Statements)
            {
                string statementResult = Visit(ss);
                if (string.IsNullOrEmpty(statementResult))
                {
                    throw new NotImplementedException($"{ss.GetType()} statements are not implemented.");
                }
                else
                {
                    sb.AppendLine(statementResult);
                }
            }

            sb.AppendLine("}");

            _value = sb.ToString();
            return sb.ToString();
        }

        public override string VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            VariableDeclarationSyntax decl = node.Declaration;
            if (decl.Variables.Count != 1)
            {
                throw new NotImplementedException();
            }

            string mappedType = _backend.CSharpToShaderType(decl.Type);
            return mappedType + " " + decl.Variables[0].Identifier + " " + Visit(decl.Variables[0].Initializer) + ";";
        }

        public override string VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            return node.EqualsToken.ToFullString() + Visit(node.Value);
        }

        public override string VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            return Visit(node.Left)
                + node.OperatorToken.ToFullString()
                + Visit(node.Right)
                + ";";
        }

        public override string VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            return Visit(node.Expression)
                + node.OperatorToken.ToFullString()
                + Visit(node.Name);
        }

        public override string VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            return Visit(node.Expression);
        }

        public override string VisitReturnStatement(ReturnStatementSyntax node)
        {
            return "return "
                + Visit(node.Expression)
                + ";";
        }

        public override string VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            IdentifierNameSyntax ins = node.Expression as IdentifierNameSyntax;
            if (ins == null)
            {
                throw new NotImplementedException("Function calls must be made through an IdentifierNameSyntax.");
            }

            SymbolInfo symbolInfo = _model.GetSymbolInfo(ins);
            string type = symbolInfo.Symbol.ContainingType.ToDisplayString();
            string method = symbolInfo.Symbol.Name;
            string functionName = _backend.CSharpToShaderFunctionName(type, method);
            return $"{functionName}({Visit(node.ArgumentList)})";
        }

        public override string VisitArgumentList(ArgumentListSyntax node)
        {
            return string.Join(", ", node.Arguments.Select(argSyntax => Visit(argSyntax)));
        }

        public override string VisitArgument(ArgumentSyntax node)
        {
            string result = Visit(node.Expression);
            if (string.IsNullOrEmpty(result))
            {
                throw new NotImplementedException($"{node.Expression.GetType()} arguments are not implemented.");
            }
            else
            {
                return result;
            }
        }

        public override string VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            SymbolInfo symbolInfo = _model.GetSymbolInfo(node.Type);
            string fullName = symbolInfo.Symbol.Name;
            string ns = symbolInfo.Symbol.ContainingNamespace.ToDisplayString();
            if (!string.IsNullOrEmpty(ns))
            {
                fullName = ns + "." + fullName;
            }
            return _backend.CSharpToShaderType(fullName) + "(" + Visit(node.ArgumentList) + ")";
        }

        public override string VisitIdentifierName(IdentifierNameSyntax node)
        {
            return node.Identifier.ToFullString();
        }

        public override string VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            return node.ToFullString();
        }

        private string GetParameterDeclList()
        {
            return string.Join(", ", _shaderFunction.Parameters.Select(pd => $"{_backend.CSharpToShaderType(pd.Type.Name)} {pd.Name}"));
        }
    }
}