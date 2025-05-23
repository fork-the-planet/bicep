// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Bicep.Core.Parsing;
using Bicep.Core.Text;

namespace Bicep.Core.Syntax;

public class UnaryTypeOperationSyntax : TypeSyntax
{
    public UnaryTypeOperationSyntax(Token operatorToken, SyntaxBase expression)
    {
        if (Operators.TokenTypeToUnaryOperator.ContainsKey(operatorToken.Type) == false)
        {
            throw new ArgumentException($"{nameof(operatorToken)} is of type '{operatorToken.Type}' which does not represent a valid unary operator.");
        }

        this.OperatorToken = operatorToken;
        this.Expression = expression;
    }

    public Token OperatorToken { get; }

    public SyntaxBase Expression { get; }

    public UnaryOperator Operator => Operators.TokenTypeToUnaryOperator[this.OperatorToken.Type];

    public override void Accept(ISyntaxVisitor visitor) => visitor.VisitUnaryTypeOperationSyntax(this);

    public override TextSpan Span => TextSpan.Between(OperatorToken, Expression);
}
