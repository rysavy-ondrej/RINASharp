// <copyright file="PexAssemblyInfo.cs" company="Brno University of Technology">Ondrej Rysavy</copyright>
using Microsoft.Pex.Framework.Coverage;
using Microsoft.Pex.Framework.Creatable;
using Microsoft.Pex.Framework.Instrumentation;
using Microsoft.Pex.Framework.Settings;
using Microsoft.Pex.Framework.Validation;

// Microsoft.Pex.Framework.Settings
[assembly: PexAssemblySettings(TestFramework = "NUnit2")]

// Microsoft.Pex.Framework.Instrumentation
[assembly: PexAssemblyUnderTest("System.Net.Rina")]
[assembly: PexInstrumentAssembly("System.Runtime.Serialization")]
[assembly: PexInstrumentAssembly("System.ServiceModel")]
[assembly: PexInstrumentAssembly("System.Core")]
[assembly: PexInstrumentAssembly("System.Threading.Tasks.Dataflow")]

// Microsoft.Pex.Framework.Creatable
[assembly: PexCreatableFactoryForDelegates]

// Microsoft.Pex.Framework.Validation
[assembly: PexAllowedContractRequiresFailureAtTypeUnderTestSurface]
[assembly: PexAllowedXmlDocumentedException]

// Microsoft.Pex.Framework.Coverage
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.Runtime.Serialization")]
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.ServiceModel")]
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.Core")]
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.Threading.Tasks.Dataflow")]

