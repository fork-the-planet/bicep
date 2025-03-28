// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Bicep.Core.Parsing;
using Bicep.Core.Text;

namespace Bicep.Core.Syntax
{
    public class ForSyntax : SyntaxBase
    {
        public ForSyntax(
            Token openSquare,
            ImmutableArray<Token> openNewlines,
            Token forKeyword,
            SyntaxBase variableSection,
            SyntaxBase inKeyword,
            SyntaxBase expression,
            SyntaxBase colon,
            SyntaxBase body,
            ImmutableArray<Token> closeNewlines,
            SyntaxBase closeSquare)
        {
            AssertTokenType(openSquare, nameof(openSquare), TokenType.LeftSquare);
            AssertKeyword(forKeyword, nameof(forKeyword), LanguageConstants.ForKeyword);
            AssertSyntaxType(variableSection, nameof(variableSection), typeof(LocalVariableSyntax), typeof(VariableBlockSyntax), typeof(SkippedTriviaSyntax));
            AssertSyntaxType(inKeyword, nameof(inKeyword), typeof(Token), typeof(SkippedTriviaSyntax));
            AssertKeyword(inKeyword as Token, nameof(inKeyword), LanguageConstants.InKeyword);
            AssertSyntaxType(colon, nameof(colon), typeof(Token), typeof(SkippedTriviaSyntax));
            AssertTokenType(colon as Token, nameof(colon), TokenType.Colon);
            AssertSyntaxType(closeSquare, nameof(closeSquare), typeof(Token), typeof(SkippedTriviaSyntax));
            AssertTokenType(closeSquare as Token, nameof(closeSquare), TokenType.RightSquare);

            this.OpenSquare = openSquare;
            this.OpenNewlines = openNewlines;
            this.ForKeyword = forKeyword;
            this.VariableSection = variableSection;
            this.InKeyword = inKeyword;
            this.Expression = expression;
            this.Colon = colon;
            this.Body = body;
            this.CloseNewlines = closeNewlines;
            this.CloseSquare = closeSquare;
        }

        public Token OpenSquare { get; }

        public ImmutableArray<Token> OpenNewlines { get; }

        public Token ForKeyword { get; }

        public SyntaxBase VariableSection { get; }

        public SyntaxBase InKeyword { get; }

        public SyntaxBase Expression { get; }

        public SyntaxBase Colon { get; }

        public SyntaxBase Body { get; }

        public ImmutableArray<Token> CloseNewlines { get; }

        public SyntaxBase CloseSquare { get; }

        public override void Accept(ISyntaxVisitor visitor) => visitor.VisitForSyntax(this);

        public override TextSpan Span => TextSpan.Between(this.OpenSquare, this.CloseSquare);

        public LocalVariableSyntax? ItemVariable => this.VariableSection switch
        {
            LocalVariableSyntax itemVariable => itemVariable,
            VariableBlockSyntax block => block.Arguments.FirstOrDefault(),
            SkippedTriviaSyntax => null,
            _ => throw new NotImplementedException($"Unexpected loop variable section type '{this.VariableSection.GetType().Name}'.")
        };

        public LocalVariableSyntax? IndexVariable => this.VariableSection switch
        {
            LocalVariableSyntax => null,
            VariableBlockSyntax block => block.Arguments.Skip(1).FirstOrDefault(),
            SkippedTriviaSyntax => null,
            _ => throw new NotImplementedException($"Unexpected loop variable section type '{this.VariableSection.GetType().Name}'.")
        };
    }
}
