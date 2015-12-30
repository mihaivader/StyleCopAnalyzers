﻿// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace StyleCop.Analyzers.DocumentationRules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using StyleCop.Analyzers.Helpers;

    /// <summary>
    /// This is the base class for analyzers which examine the <c>&lt;param&gt;</c> text of a documentation comment on an element declaration.
    /// </summary>
    internal abstract class ElementDocumentationParameterBase : DiagnosticAnalyzer
    {
        private readonly Action<CompilationStartAnalysisContext> compilationStartAction;
        private readonly Action<SyntaxNodeAnalysisContext> methodDeclarationAction;
        private readonly Action<SyntaxNodeAnalysisContext> constructorDeclarationAction;
        private readonly Action<SyntaxNodeAnalysisContext> delegateDeclarationAction;
        private readonly Action<SyntaxNodeAnalysisContext> indexerDeclarationAction;
        private readonly Action<SyntaxNodeAnalysisContext> operatorDeclarationAction;
        private readonly Action<SyntaxNodeAnalysisContext> conversionOperatorDeclarationAction;

        protected ElementDocumentationParameterBase()
        {
            this.compilationStartAction = this.HandleCompilationStart;
            this.methodDeclarationAction = this.HandleMethodDeclaration;
            this.constructorDeclarationAction = this.HandleConstructorDeclaration;
            this.delegateDeclarationAction = this.HandleDelegateDeclaration;
            this.indexerDeclarationAction = this.HandleIndexerDeclaration;
            this.operatorDeclarationAction = this.HandleOperatorDeclaration;
            this.conversionOperatorDeclarationAction = this.HandleConversionOperatorDeclaration;
        }

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(this.compilationStartAction);
        }

        /// <summary>
        /// Analyzes the top-level <c>&lt;param&gt;</c> elements of a documentation comment.
        /// </summary>
        /// <param name="context">The current analysis context.</param>
        /// <param name="syntaxList">The <see cref="XmlElementSyntax"/> or <see cref="XmlEmptyElementSyntax"/> of the node
        /// to examine.</param>
        /// <param name="completeDocumentation">The complete documentation for the declared symbol, with any
        /// <c>&lt;include&gt;</c> elements expanded. If the XML documentation comment included a <c>&lt;param&gt;</c>
        /// element, this value will be <see langword="null"/>, even if the XML documentation comment also included an
        /// <c>&lt;include&gt;</c> element.</param>
        /// <param name="diagnosticLocations">The location(s) where diagnostics, if any, should be reported.</param>
        protected abstract void HandleXmlElement(SyntaxNodeAnalysisContext context, IEnumerable<XmlNodeSyntax> syntaxList, XElement completeDocumentation, params Location[] diagnosticLocations);

        private void HandleCompilationStart(CompilationStartAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionHonorExclusions(this.methodDeclarationAction, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeActionHonorExclusions(this.constructorDeclarationAction, SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeActionHonorExclusions(this.delegateDeclarationAction, SyntaxKind.DelegateDeclaration);
            context.RegisterSyntaxNodeActionHonorExclusions(this.indexerDeclarationAction, SyntaxKind.IndexerDeclaration);
            context.RegisterSyntaxNodeActionHonorExclusions(this.operatorDeclarationAction, SyntaxKind.OperatorDeclaration);
            context.RegisterSyntaxNodeActionHonorExclusions(this.conversionOperatorDeclarationAction, SyntaxKind.ConversionOperatorDeclaration);
        }

        private void HandleMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;
            if (node.Identifier.IsMissing)
            {
                return;
            }

            this.HandleDeclaration(context, node, node.Identifier.GetLocation());
        }

        private void HandleConstructorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (ConstructorDeclarationSyntax)context.Node;
            if (node.Identifier.IsMissing)
            {
                return;
            }

            this.HandleDeclaration(context, node, node.Identifier.GetLocation());
        }

        private void HandleDelegateDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (DelegateDeclarationSyntax)context.Node;
            if (node.Identifier.IsMissing)
            {
                return;
            }

            this.HandleDeclaration(context, node, node.Identifier.GetLocation());
        }

        private void HandleIndexerDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (IndexerDeclarationSyntax)context.Node;
            if (node.ThisKeyword.IsMissing)
            {
                return;
            }

            this.HandleDeclaration(context, node, node.ThisKeyword.GetLocation());
        }

        private void HandleOperatorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (OperatorDeclarationSyntax)context.Node;
            if (node.OperatorToken.IsMissing)
            {
                return;
            }

            this.HandleDeclaration(context, node, node.OperatorToken.GetLocation());
        }

        private void HandleConversionOperatorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (ConversionOperatorDeclarationSyntax)context.Node;

            this.HandleDeclaration(context, node, node.GetLocation());
        }

        private void HandleDeclaration(SyntaxNodeAnalysisContext context, SyntaxNode node, params Location[] locations)
        {
            var documentation = node.GetDocumentationCommentTriviaSyntax();
            if (documentation == null)
            {
                // missing documentation is reported by SA1600, SA1601, and SA1602
                return;
            }

            if (documentation.Content.GetFirstXmlElement(XmlCommentHelper.InheritdocXmlTag) != null)
            {
                // Ignore nodes with an <inheritdoc/> tag.
                return;
            }

            XElement completeDocumentation = null;
            var paramXmlElements = documentation.Content.GetXmlElements(XmlCommentHelper.ParamXmlTag);
            if (!paramXmlElements.Any())
            {
                var includedDocumentation = documentation.Content.GetFirstXmlElement(XmlCommentHelper.IncludeXmlTag);
                if (includedDocumentation != null)
                {
                    var declaration = context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken);
                    var rawDocumentation = declaration?.GetDocumentationCommentXml(expandIncludes: true, cancellationToken: context.CancellationToken);
                    completeDocumentation = XElement.Parse(rawDocumentation, LoadOptions.None);
                    if (completeDocumentation.Nodes().OfType<XElement>().Any(element => element.Name == XmlCommentHelper.InheritdocXmlTag))
                    {
                        // Ignore nodes with an <inheritdoc/> tag in the included XML.
                        return;
                    }
                }
            }

            this.HandleXmlElement(context, paramXmlElements, completeDocumentation, locations);
        }
    }
}
