// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Bicep.Core.Diagnostics;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.FileSystem;
using Bicep.Core.UnitTests.Utils;
using Bicep.TextFixtures.Utils;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Bicep.Core.IntegrationTests.Scenarios
{
    /// <summary>
    /// Tests below will not test actual loading file just the fact that bicep function accepts parameters and produces expected values in ARM template output.
    /// Loading file is done via IFileResolver - here we use InMemoryFileResolver, therefore no actual file reading can be tested.
    /// Testing FileResolver is covered in a separate place. E2E Testing is done in baseline tests
    /// </summary>
    [TestClass]
    public class LoadFunctionsTests
    {
        private const string TEXT_CONTENT = @"
Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.
Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.
";
        private static readonly string B64_TEXT_CONTENT = Convert.ToBase64String(Encoding.UTF8.GetBytes(TEXT_CONTENT));
        public enum FunctionCase { loadTextContent, loadFileAsBase64, loadJsonContent, loadYamlContent }
        private static string ExpectedResult(FunctionCase function) => function switch
        {
            FunctionCase.loadTextContent => TEXT_CONTENT,
            FunctionCase.loadFileAsBase64 => B64_TEXT_CONTENT,
            _ => throw new NotSupportedException()
        };

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent)]
        [DataRow(FunctionCase.loadFileAsBase64)]
        public void LoadFunction_inVariable(FunctionCase function)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var script = " + function + @"('script.sh')

output out string = script
"),
    ("script.sh", TEXT_CONTENT));

            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            template!.Should().NotBeNull();
            var testToken = template!.SelectToken("$.variables.script");
            using (new AssertionScope())
            {
                testToken.Should().NotBeNull().And.DeepEqual(ExpectedResult(function));
            }
        }

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent)]
        [DataRow(FunctionCase.loadFileAsBase64)]
        public void LoadFunction_asPartOfObject_inVariables(FunctionCase function)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var script = {
  name: 'script'
  content: " + function + @"('script.sh')
}

output out object = script
"),
    ("script.sh", TEXT_CONTENT));

            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                template!.SelectToken("$.variables.script.content").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(ExpectedResult(function));
            }
        }

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent)]
        [DataRow(FunctionCase.loadFileAsBase64)]
        public void LoadFunction_InInterpolation_inVariable(FunctionCase function)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var message = 'Body: ${" + function + @"('message.txt')}'

output out string = message
"),
    ("message.txt", TEXT_CONTENT));

            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            template!.Should().NotBeNull();
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables['$fxv#0']").Should().NotBeNull().And.DeepEqual(ExpectedResult(function));
                template!.SelectToken("$.variables.message").Should().NotBeNull().And.DeepEqual("[format('Body: {0}', variables('$fxv#0'))]");
            }
        }

        private static CompilationHelper.CompilationResult CreateLoadTextContentTestCompilation(string encodingName)
        {
            var encoding = LanguageConstants.SupportedEncodings.TryGetValue(encodingName, out var val) ? val : Encoding.UTF8;

            var files = new Dictionary<Uri, MockFileData>
            {
                [new Uri("file:///main.bicep")] = new(@"
var message = loadTextContent('message.txt', '" + encodingName + @"')

output out string = message
"),
                [new Uri("file:///message.txt")] = new(TEXT_CONTENT, encoding),
            };

            return CompilationHelper.Compile(new(), new InMemoryFileResolver(files), files.Keys, new Uri("file:///main.bicep"));
        }

        [DataTestMethod]
        [DataRow("utf-8")]
        [DataRow("utf-16BE")]
        [DataRow("utf-16")]
        [DataRow("us-ascii")]
        [DataRow("iso-8859-1")]
        public void LoadTextContent_AcceptsAvailableEncoding(string encoding)
        {
            var (template, diags, _) = CreateLoadTextContentTestCompilation(encoding);

            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            template!.Should().NotBeNull();
            var testToken = template!.SelectToken("$.variables.message");
            using (new AssertionScope())
            {
                testToken.Should().NotBeNull().And.DeepEqual(TEXT_CONTENT);
            }
        }

        [DataTestMethod]
        [DataRow("utf")]
        [DataRow("utf-32be")]
        [DataRow("utf-32le")]
        [DataRow("utf-7")]
        [DataRow("iso-8859-2")]
        [DataRow("en-us")]
        [DataRow("")]
        public void LoadTextContent_DisallowsUnknownEncoding(string encoding)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CreateLoadTextContentTestCompilation(encoding);

            template!.Should().BeNull();
            diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP070", Diagnostics.DiagnosticLevel.Error, $"Argument of type \"'{encoding}'\" is not assignable to parameter of type \"{LanguageConstants.LoadTextContentEncodings}\".");
        }

        [DataTestMethod]
        [DataRow("utf")]
        [DataRow("iso-8859-2")]
        [DataRow("en-us")]
        [DataRow("")]
        public void LoadTextContent_DisallowsUnknownEncoding_passedFromVariable(string encoding)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CreateLoadTextContentTestCompilation(encoding);

            template!.Should().BeNull();
            diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP070", Diagnostics.DiagnosticLevel.Error, $"Argument of type \"'{encoding}'\" is not assignable to parameter of type \"{LanguageConstants.LoadTextContentEncodings}\".");
        }

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent, "var fileName = 'message.txt'", "fileName", DisplayName = "loadTextContent: variable")]
        [DataRow(FunctionCase.loadFileAsBase64, "var fileName = 'message.txt'", "fileName", DisplayName = "loadFileAsBase64: variable")]
        [DataRow(FunctionCase.loadTextContent, @"var fileNames = [
'message.txt'
]", "fileNames[0]", DisplayName = "loadTextContent: array value")]
        [DataRow(FunctionCase.loadFileAsBase64, @"var fileNames = [
'message.txt'
]", "fileNames[0]", DisplayName = "loadFileAsBase64: array value")]
        [DataRow(FunctionCase.loadTextContent, @"var files = [
 {
  name: 'message.txt'
 }
]", "files[0].name", DisplayName = "loadTextContent: object property from an array")]
        [DataRow(FunctionCase.loadFileAsBase64, @"var files = [
 {
  name: 'message.txt'
 }
]", "files[0].name", DisplayName = "loadFileAsBase64: object property from an array")]
        [DataRow(FunctionCase.loadTextContent, @"var files = [
 {
  name: 'message.txt'
  encoding: 'utf-8'
 }
]", "files[0].name", "files[0].encoding", DisplayName = "loadTextContent: object property and encoding from an array")]
        [DataRow(FunctionCase.loadTextContent, @"var encoding = 'us-ascii'", "'message.txt'", "encoding", DisplayName = "loadTextContent: encoding as variable")]
        public void LoadFunction_RequiresCompileTimeConstantArguments_Valid(FunctionCase function, string declaration, string filePath, string? encoding = null)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
" + declaration + @"
var message = " + function + @"(" + filePath + ((encoding is null) ? string.Empty : (", " + encoding)) + @")
output out string = message
"),
    ("message.txt", TEXT_CONTENT));

            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            template!.Should().NotBeNull();
            var testToken = template!.SelectToken("$.variables.message");
            using (new AssertionScope())
            {
                testToken.Should().NotBeNull().And.DeepEqual(ExpectedResult(function));
            }
        }

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent, "param fileName string = 'message.txt'", "fileName", DisplayName = "loadTextContent: parameter")]
        [DataRow(FunctionCase.loadFileAsBase64, "param fileName string = 'message.txt'", "fileName", DisplayName = "loadFileAsBase64: parameter")]
        [DataRow(FunctionCase.loadJsonContent, "param fileName string = 'message.txt'", "fileName", DisplayName = "loadJsonContent: parameter")]
        [DataRow(FunctionCase.loadTextContent, @"param fileName string = 'message.txt'
        var _fileName = fileName", "_fileName", DisplayName = "loadTextContent: variable from parameter")]
        [DataRow(FunctionCase.loadFileAsBase64, @"param fileName string = 'message.txt'
        var _fileName = fileName", "_fileName", DisplayName = "loadFileAsBase64: variable from parameter")]
        [DataRow(FunctionCase.loadJsonContent, @"param fileName string = 'message.txt'
        var _fileName = fileName", "_fileName", DisplayName = "loadJsonContent: variable from parameter")]
        [DataRow(FunctionCase.loadTextContent, @"param fileName string = 'message.txt'
        var fileNames = [
        fileName
        ]", "fileNames[0]", DisplayName = "loadTextContent: param as array value")]
        [DataRow(FunctionCase.loadFileAsBase64, @"param fileName string = 'message.txt'
        var fileNames = [
        fileName
        ]", "fileNames[0]", DisplayName = "loadFileAsBase64: param as array value")]
        [DataRow(FunctionCase.loadJsonContent, @"param fileName string = 'message.txt'
        var fileNames = [
        fileName
        ]", "fileNames[0]", DisplayName = "loadJsonContent: param as array value")]
        [DataRow(FunctionCase.loadTextContent, @"param fileName string = 'message.txt'
        var files = [
         {
          name: fileName
         }
        ]", "files[0].name", DisplayName = "loadTextContent: param as object property in array")]
        [DataRow(FunctionCase.loadFileAsBase64, @"param fileName string = 'message.txt'
        var files = [
         {
          name: fileName
         }
        ]", "files[0].name", DisplayName = "loadFileAsBase64: param as object property in array")]
        [DataRow(FunctionCase.loadJsonContent, @"param fileName string = 'message.txt'
        var files = [
         {
          name: fileName
         }
        ]", "files[0].name", DisplayName = "loadJsonContent: param as object property in array")]
        [DataRow(FunctionCase.loadTextContent, @"param encoding string = 'us-ascii'
        var files = [
         {
          name: 'message.txt'
          encoding: encoding
         }
        ]", "files[0].name", "files[0].encoding", DisplayName = "loadTextContent: encoding param as object property in array")]
        [DataRow(FunctionCase.loadJsonContent, @"param encoding string = 'us-ascii'
        param path string = '$'
        var files = [
         {
          name: 'message.json'
          path: path
          encoding: encoding
         }
        ]", "files[0].name", "files[0].path", DisplayName = "loadJsonContent: path param as object property in array")]
        [DataRow(FunctionCase.loadJsonContent, @"param encoding string = 'us-ascii'
        var files = [
         {
          name: 'message.json'
          path: '$'
          encoding: encoding
         }
        ]", "files[0].name", "'$'", "files[0].encoding", DisplayName = "loadJsonContent: encoding param as object property in array")]
        [DataRow(FunctionCase.loadYamlContent, @"param encoding string = 'us-ascii'
        var files = [
            {
                name: 'message.yaml'
                path: '$'
                encoding: encoding
            }
        ]", "files[0].name", "'$'", "files[0].encoding", DisplayName = "loadYamlContent: encoding param as object property in array")]
        public void LoadFunction_RequiresCompileTimeConstantArguments_Invalid(FunctionCase function, string declaration, params string[] args)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
" + declaration + @"
var message = 'Body: ${" + function + @"(" + string.Join(", ", args) + @")}'
"),
    ("message.txt", TEXT_CONTENT));

            template!.Should().BeNull();
            diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP032", Diagnostics.DiagnosticLevel.Error, "The value must be a compile-time constant.");
        }

        private const string LOGIC_APP = @"
{
  ""$schema"": ""https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {},
  ""triggers"": {
      ""manual"": {
          ""type"": ""Request"",
          ""kind"": ""Http"",
          ""inputs"": {
              ""schema"": {}
          }
      }
  },
  ""actions"": {
      ""Delay"": {
          ""runAfter"": {},
          ""type"": ""Wait"",
          ""inputs"": {
              ""interval"": {
                  ""count"": 10,
                  ""unit"": ""Second""
              }
          }
      }
  },
  ""outputs"": {}
}";
        [TestMethod]
        public void LoadTextContent_RequiresCompileTimeConstantArguments_Loop_Invalid_Interpolation()
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var apps = [
  'logicApp1'
  'logicApp2'
]
resource logicApps 'Microsoft.Logic/workflows@2019-05-01' = [ for app in apps :{
  name: app
  location: resourceGroup().location
  properties: {
    definition: json(loadTextContent('${app}.json'))
  }
}]"),
    ("logicApp1.json", LOGIC_APP), ("logicApp2.json", LOGIC_APP));

            template!.Should().BeNull();
            diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP032", Diagnostics.DiagnosticLevel.Error, "The value must be a compile-time constant.");
        }

        [TestMethod]
        public void LoadTextContent_RequiresCompileTimeConstantArguments_Loop_Invalid_indexAccess()
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var apps = [
  {
    name: 'logicApp1'
    file: 'logicApp1.json'
  }
  {
    name: 'logicApp2'
    file: 'logicApp2.json'
  }
]
resource logicApps 'Microsoft.Logic/workflows@2019-05-01' = [ for (app, i) in apps :{
  name: app.name
  location: resourceGroup().location
  properties: {
    definition: json(loadTextContent(app.file))
  }
}]"),
    ("logicApp1.json", LOGIC_APP), ("logicApp2.json", LOGIC_APP));

            template!.Should().BeNull();
            diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP032", Diagnostics.DiagnosticLevel.Error, "The value must be a compile-time constant.");
        }

        [TestMethod]
        public void LoadTextContent_RequiresCompileTimeConstantArguments_Loop_Valid()
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var apps = [
  {
    name: 'logicApp1'
    file: loadTextContent('logicApp1.json')
  }
  {
    name: 'logicApp2'
    file: loadTextContent('logicApp2.json')
  }
]
resource logicApps 'Microsoft.Logic/workflows@2019-05-01' = [ for (app, i) in apps :{
  name: app.name
  location: resourceGroup().location
  properties: {
    definition: json(app.file)
  }
}]"),
    ("logicApp1.json", LOGIC_APP), ("logicApp2.json", LOGIC_APP));

            template!.Should().NotBeNull();
            diags.ExcludingLinterDiagnostics().Should().BeEmpty();
        }

        public static IEnumerable<object[]> LoadFunction_InvalidPath_Data
        {
            get
            {
                foreach (var function in new[] { FunctionCase.loadTextContent, FunctionCase.loadFileAsBase64 })
                {
                    yield return new object[] { function, "" };
                    yield return new object[] { function, " " };
                    yield return new object[] { function, "\t" };
                    yield return new object[] { function, "/script.sh" };
                    yield return new object[] { function, ".\\script.sh" };
                    yield return new object[] { function, "https://storage.azure.com/script.sh" };
                    yield return new object[] { function, "github://azure/bicep/@main/samples/script.sh" };
                    yield return new object[] { function, "ssh://root@host:/data/script.sh" };
                    yield return new object[] { function, "../" };
                    yield return new object[] { function, "dir\\..\\script.sh" };
                }
            }
        }
        [DataTestMethod]
        [DynamicData(nameof(LoadFunction_InvalidPath_Data), DynamicDataSourceType.Property)]
        public void LoadFunction_InvalidPath(FunctionCase function, string invalidPath)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var script = " + function + @"('" + invalidPath + @"')

output out string = script
"),
    ("script.sh", TEXT_CONTENT));

            template!.Should().BeNull();
        }

        [DataTestMethod]
        [DataRow(FunctionCase.loadTextContent)]
        [DataRow(FunctionCase.loadFileAsBase64)]
        [DataRow(FunctionCase.loadJsonContent)]
        [DataRow(FunctionCase.loadYamlContent)]
        public void LoadFunction_FileDoesNotExist(FunctionCase function)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var script = " + function + @"('script.cmd')

output out string = script
"),
    ("script.sh", TEXT_CONTENT));

            template!.Should().BeNull();
            var scriptFilePath = InMemoryFileResolver.GetFileUri("/path/to/script.cmd").LocalPath;
            diags.Should().ContainDiagnostic("BCP091", Diagnostics.DiagnosticLevel.Error, $"An error occurred reading file. Could not find file '{scriptFilePath}'.");
        }

        /**** loadJsonContent ****/
        private const string TEST_JSON = @"{
  ""propString"": ""propStringValue"",
  ""propBoolTrue"": true,
  ""propBoolFalse"": false,
  ""propNull"": null,
  ""propInt"" : 1073741824,
  ""propIntNegative"" : -1073741824,
  ""propBigInt"" : 4611686018427387904,
  ""propBigIntNegative"" : -4611686018427387904,
  ""propFloat"" : 1.618033988749894,
  ""propFloatNegative"" : -1.618033988749894,
  ""propArrayString"" : [
    ""stringArray1"",
    ""stringArray2"",
    ""stringArray3"",
  ],
  ""propArrayInt"" : [
    153584335,
    -5889645,
    4611686018427387904,
  ],
  ""propArrayFloat"" : [
    1.61803398874,
    3.14159265359,
    -1.73205080757,
  ],
  ""propObject"" : {
      ""subObjectPropString"": ""subObjectPropStringValue"",
      ""subObjectPropBoolTrue"": true,
      ""subObjectPropBoolFalse"": false,
      ""subObjectPropNull"": null,
      ""subObjectPropInt"" : 1234542113245,
      ""subObjectPropFloat"" : 1.618033988749894,
      ""subObjectPropArrayString"" : [
        ""subObjectStringArray1"",
        ""subObjectStringArray2"",
        ""subObjectStringArray3"",
      ],
      ""subObjectPropArrayInt"" : [
        153584335,
        -5889645,
        4611686018427387904,
      ],
      ""subObjectPropArrayFloat"" : [
        1.61803398874,
        3.14159265359,
        -1.73205080757,
      ]
  },
  ""armExpression"": ""[createObject('armObjProp', 'armObjValue')]""
}";
        private const string TEST_JSON_ARM = @"{
  ""propString"": ""propStringValue"",
  ""propBoolTrue"": true,
  ""propBoolFalse"": false,
  ""propNull"": null,
  ""propInt"": 1073741824,
  ""propIntNegative"": -1073741824,
  ""propBigInt"": 4611686018427387904,
  ""propBigIntNegative"": -4611686018427387904,
  ""propFloat"": ""[json('1.618033988749894')]"",
  ""propFloatNegative"": ""[json('-1.618033988749894')]"",
  ""propArrayString"": [
    ""stringArray1"",
    ""stringArray2"",
    ""stringArray3""
  ],
  ""propArrayInt"": [
    153584335,
    -5889645,
    4611686018427387904
  ],
  ""propArrayFloat"": [
    ""[json('1.61803398874')]"",
    ""[json('3.14159265359')]"",
    ""[json('-1.73205080757')]""
  ],
  ""propObject"": {
    ""subObjectPropString"": ""subObjectPropStringValue"",
    ""subObjectPropBoolTrue"": true,
    ""subObjectPropBoolFalse"": false,
    ""subObjectPropNull"": null,
    ""subObjectPropInt"": 1234542113245,
    ""subObjectPropFloat"": ""[json('1.618033988749894')]"",
    ""subObjectPropArrayString"": [
      ""subObjectStringArray1"",
      ""subObjectStringArray2"",
      ""subObjectStringArray3""
    ],
    ""subObjectPropArrayInt"": [
      153584335,
      -5889645,
      4611686018427387904
    ],
    ""subObjectPropArrayFloat"": [
      ""[json('1.61803398874')]"",
      ""[json('3.14159265359')]"",
      ""[json('-1.73205080757')]""
    ]
  },
  ""armExpression"": ""[[createObject('armObjProp', 'armObjValue')]""
}";

        [TestMethod]
        public void LoadJsonFunction()
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var fileObj = loadJsonContent('file.json')
"),
    ("file.json", TEST_JSON));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_JSON_ARM));
            }
        }

        [DataTestMethod]
        [DataRow("$")]
        [DataRow(".propObject")]
        [DataRow(".propArrayFloat[0]")]
        [DataRow(".propObject.subObjectPropString")]
        [DataRow(".propObject.subObjectPropFloat")]
        [DataRow(".propObject.subObjectPropFloat")]
        [DataRow(".propObject.subObjectPropArrayInt[0]")]
        public void LoadJsonFunction_withPath(string path)
        {
            var (template, diags, _) = CompilationHelper.Compile(
                ("main.bicep", @"
var fileObj = loadJsonContent('file.json', '" + path + @"')
"),
                ("file.json", TEST_JSON));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_JSON_ARM).SelectToken(path)!);
            }
        }

        [TestMethod]
        [DataRow(".")]
        [DataRow("[]")]
        [DataRow("[0]")]
        [DataRow("$.")]
        [DataRow(".propObject[0]")]
        [DataRow(".propArrayFloat[5]")]
        [DataRow(".propObject/subObjectPropString")]
        [DataRow(".propObj")]
        public void LoadJsonFunction_withPath_errorWhenPathInvalidOrDoesNotExist(string path)
        {
            var (template, diags, _) = CompilationHelper.Compile(
                ("main.bicep", @"
var fileObj = loadJsonContent('file.json', '" + path + @"')
"),
                ("file.json", TEST_JSON));

            using (new AssertionScope())
            {
                template!.Should().BeNull();
                diags.ExcludingLinterDiagnostics().Should().HaveDiagnostics(new[] { ("BCP235", DiagnosticLevel.Error, "Specified JSONPath does not exist in the given file or is invalid.") });
            }
        }

        private static CompilationHelper.CompilationResult CreateLoadJsonContentTestCompilation(string encodingName)
        {
            var encoding = LanguageConstants.SupportedEncodings.TryGetValue(encodingName, out var val) ? val : Encoding.UTF8;

            var files = new Dictionary<Uri, MockFileData>
            {
                [new Uri("file:///main.bicep")] = new(@"
var fileObj = loadJsonContent('file.json', '$', '" + encodingName + @"')
"),
                [new Uri("file:///file.json")] = new(TEST_JSON, encoding),
            };

            return CompilationHelper.Compile(new(), new InMemoryFileResolver(files), files.Keys, new Uri("file:///main.bicep"));
        }

        [DataTestMethod]
        [DataRow("utf-8")]
        [DataRow("utf-16BE")]
        [DataRow("utf-16")]
        [DataRow("us-ascii")]
        [DataRow("iso-8859-1")]
        public void LoadJsonContent_AcceptsAvailableEncoding(string encoding)
        {
            var (template, diags, _) = CreateLoadJsonContentTestCompilation(encoding);

            using (new AssertionScope())
            {
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
                template!.Should().NotBeNull();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_JSON_ARM));
            }
        }

        [DataTestMethod]
        [DataRow("utf")]
        [DataRow("utf-32be")]
        [DataRow("utf-32le")]
        [DataRow("utf-7")]
        [DataRow("iso-8859-2")]
        [DataRow("en-us")]
        [DataRow("")]
        public void LoadJsonContent_DisallowsUnknownEncoding(string encoding)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CreateLoadJsonContentTestCompilation(encoding);

            using (new AssertionScope())
            {
                template!.Should().BeNull();
                diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP070", Diagnostics.DiagnosticLevel.Error, $"Argument of type \"'{encoding}'\" is not assignable to parameter of type \"{LanguageConstants.LoadTextContentEncodings}\".");
            }
        }

        /// <summary>
        /// https://github.com/Azure/bicep/issues/7208
        /// </summary>
        [TestMethod]
        public void LoadJsonFunction_withPath_selectingMultipleTokens()
        {
            var (template, diags, _) = CompilationHelper.Compile(
                ("main.bicep", @"
var fileObj = loadJsonContent('file.json',  '.products[?(@.price > 3)].name')
"),
                ("file.json", @"
{
    ""products"" : [
        {
            ""name"": ""pizza"",
            ""price"": 5.00
        },
        {
            ""name"": ""salad"",
            ""price"": 3.99
        },
        {
            ""name"": ""bread"",
            ""price"": 2.00
        }
    ]
}
"));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(new JArray("pizza", "salad"));
            }
        }

        /// <summary>
        /// https://github.com/Azure/bicep/issues/7208
        /// </summary>
        [TestMethod]
        public void LoadJsonFunction_SupportsCommentsInJsonFile()
        {
            var (template, diags, _) = CompilationHelper.Compile(
                ("main.bicep", @"
var fileObj = loadJsonContent('file.json')
"),
                ("file.json", @"
//file
{
    ""products"" : /* block-comment */ [
        { //item-1
            ""name"": ""pizza"", //the-name
            ""price"": 5.00
        },
        //---
        { //item-2
            ""name"": ""pizza"",
            ""price"": 4.99 //price
        }
    ],
    ""ints"": [
        1, /*comment*/ 2,3,4,5,
        // second-half
        6,7,8,9,10
    ]
}
"));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(@"
{
    ""products"" : [
        {
            ""name"": ""pizza"",
            ""price"": ""[json('5')]""
        },
        {
            ""name"": ""pizza"",
            ""price"": ""[json('4.99')]""
        }
    ],
    ""ints"": [1,2,3,4,5,6,7,8,9,10]
}
"));
            }
        }

        [TestMethod]
        public async Task LoadJsonFunction_LocalDeploy_NoCharacterCountLimit()
        {
            var result = await TestCompiler
                .ForMockFileSystemCompilation()
                .Compile(
                    ("main.bicep", """
                        var fileObj = loadJsonContent('file.json')
                        """),
                    ("bicepconfig.json", """
                        {
                          "experimentalFeaturesEnabled": {
                            "localDeploy": true
                          }
                        }
                        """),
                    ("file.json", $$"""
                          "long" : "{{new string('x', LanguageConstants.MaxJsonFileCharacterLimit + 1)}}"
                        }
                        """));

            result.Diagnostics.ExcludingLinterDiagnostics().Should().BeEmpty();
        }

        /**** loadYamlContent ****/
        private const string TEST_YAML = @"propString: propStringValue
propBoolTrue: true
propBoolFalse: false
propNull: null
propInt: 1073741824
propIntNegative: -1073741824
propBigInt : 4611686018427387904
propBigIntNegative : -4611686018427387904
propFloat : 1.618033988749894
propFloatNegative : -1.618033988749894
propArrayString :
- stringArray1
- stringArray2
- stringArray3
propArrayInt :
- 153584335
- -5889645
- 4611686018427387904
propArrayFloat :
- 1.61803398874
- 3.14159265359
- -1.73205080757
propObject :
    subObjectPropString: subObjectPropStringValue
    subObjectPropBoolTrue: true
    subObjectPropBoolFalse: false
    subObjectPropNull: null
    subObjectPropInt : 1234542113245
    subObjectPropFloat : 1.618033988749894
    subObjectPropArrayString :
    - subObjectStringArray1
    - subObjectStringArray2
    - subObjectStringArray3
    subObjectPropArrayInt :
    - 153584335
    - -5889645
    - 4611686018427387904
    subObjectPropArrayFloat :
    - 1.61803398874
    - 3.14159265359
    - -1.73205080757
";

        private const string TEST_YAML_ARM = @"{
  ""propString"": ""propStringValue"",
  ""propBoolTrue"": true,
  ""propBoolFalse"": false,
  ""propNull"": null,
  ""propInt"": 1073741824,
  ""propIntNegative"": -1073741824,
  ""propBigInt"": 4611686018427387904,
  ""propBigIntNegative"": -4611686018427387904,
  ""propFloat"": ""[json('1.618033988749894')]"",
  ""propFloatNegative"": ""[json('-1.618033988749894')]"",
  ""propArrayString"": [
    ""stringArray1"",
    ""stringArray2"",
    ""stringArray3""
  ],
  ""propArrayInt"": [
    153584335,
    -5889645,
    4611686018427387904
  ],
  ""propArrayFloat"": [
    ""[json('1.61803398874')]"",
    ""[json('3.14159265359')]"",
    ""[json('-1.73205080757')]""
  ],
  ""propObject"": {
    ""subObjectPropString"": ""subObjectPropStringValue"",
    ""subObjectPropBoolTrue"": true,
    ""subObjectPropBoolFalse"": false,
    ""subObjectPropNull"": null,
    ""subObjectPropInt"": 1234542113245,
    ""subObjectPropFloat"": ""[json('1.618033988749894')]"",
    ""subObjectPropArrayString"": [
      ""subObjectStringArray1"",
      ""subObjectStringArray2"",
      ""subObjectStringArray3""
    ],
    ""subObjectPropArrayInt"": [
      153584335,
      -5889645,
      4611686018427387904
    ],
    ""subObjectPropArrayFloat"": [
      ""[json('1.61803398874')]"",
      ""[json('3.14159265359')]"",
      ""[json('-1.73205080757')]""
    ]
  }
}";
        [TestMethod]
        public void LoadYamlFunction()
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var fileObj = loadYamlContent('file.yaml')
"),
    ("file.yaml", TEST_YAML));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_YAML_ARM));
            }
        }

        [DataTestMethod]
        [DataRow("$")]
        [DataRow(".propObject")]
        [DataRow(".propArrayFloat[0]")]
        [DataRow(".propObject.subObjectPropString")]
        [DataRow(".propObject.subObjectPropFloat")]
        [DataRow(".propObject.subObjectPropFloat")]
        [DataRow(".propObject.subObjectPropArrayInt[0]")]
        public void LoadYamlFunction_withPath(string path)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var fileObj = loadYamlContent('file.yaml', '" + path + @"')
"),
    ("file.yaml", TEST_YAML));

            using (new AssertionScope())
            {
                template!.Should().NotBeNull();
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_YAML_ARM).SelectToken(path)!);
            }
        }

        [TestMethod]
        [DataRow(".")]
        [DataRow("[]")]
        [DataRow("[0]")]
        [DataRow("$.")]
        [DataRow(".propObject[0]")]
        [DataRow(".propArrayFloat[5]")]
        [DataRow(".propObject/subObjectPropString")]
        [DataRow(".propObj")]
        public void LoadYamlFunction_withPath_errorWhenPathInvalidOrDoesNotExist(string path)
        {
            var (template, diags, _) = CompilationHelper.Compile(
    ("main.bicep", @"
var fileObj = loadYamlContent('file.yaml', '" + path + @"')
"),
    ("file.yaml", TEST_YAML));

            using (new AssertionScope())
            {
                template!.Should().BeNull();
                diags.ExcludingLinterDiagnostics().Should().HaveDiagnostics(new[] { ("BCP235", DiagnosticLevel.Error, "Specified JSONPath does not exist in the given file or is invalid.") });
            }
        }

        private static CompilationHelper.CompilationResult CreateLoadYamlContentTestCompilation(string encodingName)
        {
            var encoding = LanguageConstants.SupportedEncodings.TryGetValue(encodingName, out var val) ? val : Encoding.UTF8;

            var files = new Dictionary<Uri, MockFileData>
            {
                [new Uri("file:///main.bicep")] = new(@"
var fileObj = loadYamlContent('file.yaml', '$', '" + encodingName + @"')
"),
                [new Uri("file:///file.yaml")] = new(TEST_YAML, encoding),
            };

            return CompilationHelper.Compile(new(), new InMemoryFileResolver(files), files.Keys, new Uri("file:///main.bicep"));
        }

        [DataTestMethod]
        [DataRow("utf-8")]
        [DataRow("utf-16BE")]
        [DataRow("utf-16")]
        [DataRow("us-ascii")]
        [DataRow("iso-8859-1")]
        public void LoadYamlContent_AcceptsAvailableEncoding(string encoding)
        {
            var (template, diags, _) = CreateLoadYamlContentTestCompilation(encoding);

            using (new AssertionScope())
            {
                diags.ExcludingLinterDiagnostics().Should().BeEmpty();
                template!.Should().NotBeNull();
            }
            using (new AssertionScope())
            {
                template!.SelectToken("$.variables.fileObj").Should().DeepEqual("[variables('$fxv#0')]");
                template!.SelectToken("$.variables['$fxv#0']").Should().DeepEqual(JToken.Parse(TEST_YAML_ARM));
            }
        }

        [DataTestMethod]
        [DataRow("utf")]
        [DataRow("utf-32be")]
        [DataRow("utf-32le")]
        [DataRow("utf-7")]
        [DataRow("iso-8859-2")]
        [DataRow("en-us")]
        [DataRow("")]
        public void LoadYamlContent_DisallowsUnknownEncoding(string encoding)
        {
            //notice - here we will not test actual loading file with given encoding - just the fact that bicep function accepts all .NET available encodings
            var (template, diags, _) = CreateLoadYamlContentTestCompilation(encoding);

            using (new AssertionScope())
            {
                template!.Should().BeNull();
                diags.ExcludingLinterDiagnostics().Should().ContainSingleDiagnostic("BCP070", Diagnostics.DiagnosticLevel.Error, $"Argument of type \"'{encoding}'\" is not assignable to parameter of type \"{LanguageConstants.LoadTextContentEncodings}\".");
            }
        }

    }
}
