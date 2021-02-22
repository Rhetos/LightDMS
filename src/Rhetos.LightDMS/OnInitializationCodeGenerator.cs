/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.ComponentModel.Composition;
using Rhetos.Compiler;
using Rhetos.Extensibility;
using Rhetos.Dsl;
using Rhetos.Dom.DefaultConcepts;

namespace Rhetos.LightDMS
{
#pragma warning disable CS0618 // Type or member is obsolete
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
#pragma warning restore CS0618 // Type or member is obsolete
}
