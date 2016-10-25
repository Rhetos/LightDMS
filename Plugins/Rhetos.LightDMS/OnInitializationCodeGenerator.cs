using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhetos.Dsl.DefaultConcepts;
using System.Globalization;
using System.ComponentModel.Composition;
using Rhetos.Compiler;
using Rhetos.Extensibility;
using Rhetos.Dsl;
using Rhetos.Dom.DefaultConcepts;

namespace Rhetos.LightDMS
{
    [Export(typeof(IConceptCodeGenerator))]
    [ExportMetadata(MefProvider.Implements, typeof(OnInitializationInfo))]
    public class OnInitializationCodeGenerator : IConceptCodeGenerator
    {
        public void GenerateCode(IConceptInfo conceptInfo, ICodeBuilder codeBuilder)
        {
            var info = (OnInitializationInfo)conceptInfo;
            codeBuilder.InsertCode(GetSnippet(info), WritableOrmDataStructureCodeGenerator.InitializationTag, info.SaveMethod.Entity);
        }

        private string GetSnippet(OnInitializationInfo info)
        {
            return string.Format(
@"                {{ // {0}
                    {1}
                }}

",
                info.RuleName, info.CsCodeSnippet.Trim());
        }
    }
}
