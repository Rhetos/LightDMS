using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhetos.Dsl;
using System.ComponentModel.Composition;
using Rhetos.Dsl.DefaultConcepts;

namespace Rhetos.LightDMS
{
    [Export(typeof(IConceptInfo))]
    [ConceptKeyword("OnInitialization")]
    public class OnInitializationInfo : IConceptInfo
    {
        [ConceptKey]
        public SaveMethodInfo SaveMethod { get; set; }

        /// <summary>
        /// Name of this business rule, unique among this entity's initializations.
        /// </summary>
        [ConceptKey]
        public string RuleName { get; set; }

        /// <summary>
        /// Available variables in this context:
        ///     _executionContext,
        ///     checkUserPermissions (whether the Save command is called directly by client through a web API)
        ///     insertedNew (array of new items),
        ///     updatedNew (array of new items - update),
        ///     deletedIds (array of items to be deleted).
        /// In current state old items are not loaded. These are bare data provided to save method.
        /// Throw Rhetos.UserException("message to the user") if the Save command should be canceled and all changes rolled back.
        /// See <see cref="WritableOrmDataStructureCodeGenerator.InitializationTag">WritableOrmDataStructureCodeGenerator.InitializationTag</see> for more info.
        /// </summary>
        public string CsCodeSnippet { get; set; }
    }
}
