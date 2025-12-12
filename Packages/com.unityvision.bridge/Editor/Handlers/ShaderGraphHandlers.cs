// ============================================================================
// UnityVision Bridge - ShaderGraph Handlers
// Handlers for inspecting and creating ShaderGraph assets
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class ShaderGraphHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetShaderGraphInfoRequest
        {
            public string assetPath;
        }

        [Serializable]
        public class GetShaderGraphInfoResponse
        {
            public string path;
            public string name;
            public string graphType; // "PBR", "Unlit", "Sprite", etc.
            public List<ShaderGraphNode> nodes;
            public List<ShaderGraphProperty> properties;
            public List<ShaderGraphKeyword> keywords;
            public int nodeCount;
            public int connectionCount;
        }

        [Serializable]
        public class ShaderGraphNode
        {
            public string id;
            public string type;
            public string name;
            public Vector2Data position;
            public List<string> inputPorts;
            public List<string> outputPorts;
        }

        [Serializable]
        public class ShaderGraphProperty
        {
            public string id;
            public string name;
            public string displayName;
            public string type; // "Color", "Texture2D", "Float", "Vector2", etc.
            public string defaultValue;
            public bool exposed;
        }

        [Serializable]
        public class ShaderGraphKeyword
        {
            public string id;
            public string name;
            public string displayName;
            public bool isBuiltIn;
        }

        [Serializable]
        public class ListShaderGraphsRequest
        {
            public string folder;
            public bool includePackages;
        }

        [Serializable]
        public class ListShaderGraphsResponse
        {
            public List<ShaderGraphAssetInfo> shaderGraphs;
            public int totalCount;
        }

        [Serializable]
        public class ShaderGraphAssetInfo
        {
            public string path;
            public string name;
            public string graphType;
        }

        [Serializable]
        public class CreateShaderGraphRequest
        {
            public string path;
            public string template; // "PBR", "Unlit", "Sprite", "FullscreenEffect"
            public string name;
            public List<PropertyDefinition> properties;
        }

        [Serializable]
        public class PropertyDefinition
        {
            public string name;
            public string type; // "Color", "Texture2D", "Float", "Vector2", "Vector3", "Vector4"
            public string defaultValue;
        }

        [Serializable]
        public class CreateShaderGraphResponse
        {
            public bool success;
            public string path;
            public string message;
        }

        [Serializable]
        public class SetShaderGraphPropertyRequest
        {
            public string assetPath;
            public string propertyName;
            public string value;
        }

        [Serializable]
        public class AddShaderGraphNodeRequest
        {
            public string assetPath;
            public string nodeType;
            public float positionX;
            public float positionY;
        }

        [Serializable]
        public class AddShaderGraphNodeResponse
        {
            public bool success;
            public string nodeId;
            public string message;
        }

        [Serializable]
        public class ListShaderGraphNodeTypesResponse
        {
            public List<NodeTypeInfo> nodeTypes;
            public int totalCount;
        }

        [Serializable]
        public class NodeTypeInfo
        {
            public string category;
            public string name;
            public string fullName;
            public string description;
        }

        #endregion

        #region Handlers

        public static RpcResponse GetShaderGraphInfo(RpcRequest request)
        {
            var req = request.GetParams<GetShaderGraphInfoRequest>();

            if (string.IsNullOrEmpty(req.assetPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "assetPath is required");
            }

            if (!req.assetPath.EndsWith(".shadergraph"))
            {
                return RpcResponse.Failure("INVALID_ASSET", "Asset must be a .shadergraph file");
            }

            if (!File.Exists(req.assetPath))
            {
                // Try with Assets prefix
                var fullPath = Path.Combine(Application.dataPath, "..", req.assetPath);
                if (!File.Exists(fullPath))
                {
                    return RpcResponse.Failure("NOT_FOUND", $"ShaderGraph not found: {req.assetPath}");
                }
            }

            try
            {
                var json = File.ReadAllText(req.assetPath);
                var graphData = JObject.Parse(json);

                var response = new GetShaderGraphInfoResponse
                {
                    path = req.assetPath,
                    name = Path.GetFileNameWithoutExtension(req.assetPath),
                    nodes = new List<ShaderGraphNode>(),
                    properties = new List<ShaderGraphProperty>(),
                    keywords = new List<ShaderGraphKeyword>()
                };

                // Detect graph type from master node or target
                response.graphType = DetectGraphType(graphData);

                // Parse nodes
                var nodesArray = graphData["m_SerializedNodes"] as JArray;
                if (nodesArray != null)
                {
                    foreach (var nodeToken in nodesArray)
                    {
                        var nodeJson = nodeToken["JSONnodeData"]?.ToString();
                        if (!string.IsNullOrEmpty(nodeJson))
                        {
                            try
                            {
                                var nodeData = JObject.Parse(nodeJson);
                                var node = new ShaderGraphNode
                                {
                                    id = nodeData["m_ObjectId"]?.ToString() ?? "",
                                    type = nodeData["m_Type"]?.ToString()?.Split(',')[0] ?? "Unknown",
                                    name = nodeData["m_Name"]?.ToString() ?? "",
                                    inputPorts = new List<string>(),
                                    outputPorts = new List<string>()
                                };

                                // Get position
                                var drawState = nodeData["m_DrawState"];
                                if (drawState != null)
                                {
                                    var pos = drawState["m_Position"];
                                    if (pos != null)
                                    {
                                        node.position = new Vector2Data
                                        {
                                            X = pos["x"]?.Value<float>() ?? 0,
                                            Y = pos["y"]?.Value<float>() ?? 0
                                        };
                                    }
                                }

                                response.nodes.Add(node);
                            }
                            catch { }
                        }
                    }
                }

                response.nodeCount = response.nodes.Count;

                // Parse properties
                var propsArray = graphData["m_SerializedProperties"] as JArray;
                if (propsArray != null)
                {
                    foreach (var propToken in propsArray)
                    {
                        var propJson = propToken["JSONnodeData"]?.ToString();
                        if (!string.IsNullOrEmpty(propJson))
                        {
                            try
                            {
                                var propData = JObject.Parse(propJson);
                                var prop = new ShaderGraphProperty
                                {
                                    id = propData["m_ObjectId"]?.ToString() ?? "",
                                    name = propData["m_Name"]?.ToString() ?? "",
                                    displayName = propData["m_DisplayName"]?.ToString() ?? "",
                                    type = propData["m_Type"]?.ToString()?.Split(',')[0]?.Replace("UnityEditor.ShaderGraph.", "") ?? "Unknown",
                                    exposed = propData["m_Exposed"]?.Value<bool>() ?? true
                                };

                                response.properties.Add(prop);
                            }
                            catch { }
                        }
                    }
                }

                // Parse keywords
                var keywordsArray = graphData["m_SerializedKeywords"] as JArray;
                if (keywordsArray != null)
                {
                    foreach (var kwToken in keywordsArray)
                    {
                        var kwJson = kwToken["JSONnodeData"]?.ToString();
                        if (!string.IsNullOrEmpty(kwJson))
                        {
                            try
                            {
                                var kwData = JObject.Parse(kwJson);
                                var kw = new ShaderGraphKeyword
                                {
                                    id = kwData["m_ObjectId"]?.ToString() ?? "",
                                    name = kwData["m_Name"]?.ToString() ?? "",
                                    displayName = kwData["m_DisplayName"]?.ToString() ?? "",
                                    isBuiltIn = kwData["m_IsBuiltIn"]?.Value<bool>() ?? false
                                };

                                response.keywords.Add(kw);
                            }
                            catch { }
                        }
                    }
                }

                // Count connections
                var edgesArray = graphData["m_SerializedEdges"] as JArray;
                response.connectionCount = edgesArray?.Count ?? 0;

                return RpcResponse.Success(response);
            }
            catch (Exception e)
            {
                return RpcResponse.Failure("PARSE_ERROR", $"Failed to parse ShaderGraph: {e.Message}");
            }
        }

        public static RpcResponse ListShaderGraphs(RpcRequest request)
        {
            var req = request.GetParams<ListShaderGraphsRequest>();

            var searchFolders = new List<string> { "Assets" };
            if (!string.IsNullOrEmpty(req.folder))
            {
                searchFolders = new List<string> { req.folder };
            }
            if (req.includePackages)
            {
                searchFolders.Add("Packages");
            }

            var guids = AssetDatabase.FindAssets("t:Shader", searchFolders.ToArray());
            var shaderGraphs = new List<ShaderGraphAssetInfo>();

            // Find .shadergraph files
            var allAssets = AssetDatabase.GetAllAssetPaths();
            foreach (var path in allAssets)
            {
                if (!path.EndsWith(".shadergraph")) continue;
                if (!string.IsNullOrEmpty(req.folder) && !path.StartsWith(req.folder)) continue;
                if (!req.includePackages && path.StartsWith("Packages/")) continue;

                var info = new ShaderGraphAssetInfo
                {
                    path = path,
                    name = Path.GetFileNameWithoutExtension(path),
                    graphType = "Unknown"
                };

                // Try to detect type
                try
                {
                    var json = File.ReadAllText(path);
                    var graphData = JObject.Parse(json);
                    info.graphType = DetectGraphType(graphData);
                }
                catch { }

                shaderGraphs.Add(info);
            }

            return RpcResponse.Success(new ListShaderGraphsResponse
            {
                shaderGraphs = shaderGraphs,
                totalCount = shaderGraphs.Count
            });
        }

        public static RpcResponse CreateShaderGraph(RpcRequest request)
        {
            var req = request.GetParams<CreateShaderGraphRequest>();

            if (string.IsNullOrEmpty(req.path))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "path is required");
            }

            if (!req.path.EndsWith(".shadergraph"))
            {
                req.path += ".shadergraph";
            }

            var template = req.template?.ToLower() ?? "pbr";
            var graphJson = GenerateShaderGraphTemplate(template, req.name ?? "NewShader", req.properties);

            if (string.IsNullOrEmpty(graphJson))
            {
                return RpcResponse.Failure("INVALID_TEMPLATE", $"Unknown template: {template}. Valid: PBR, Unlit, Sprite");
            }

            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(req.path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(req.path, graphJson);
                AssetDatabase.Refresh();

                return RpcResponse.Success(new CreateShaderGraphResponse
                {
                    success = true,
                    path = req.path,
                    message = $"Created {template} ShaderGraph at {req.path}"
                });
            }
            catch (Exception e)
            {
                return RpcResponse.Failure("CREATE_FAILED", $"Failed to create ShaderGraph: {e.Message}");
            }
        }

        public static RpcResponse ListShaderGraphNodeTypes(RpcRequest request)
        {
            // Common ShaderGraph node types organized by category
            var nodeTypes = new List<NodeTypeInfo>
            {
                // Input
                new NodeTypeInfo { category = "Input/Basic", name = "Color", fullName = "ColorNode", description = "A constant color value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Float", fullName = "Vector1Node", description = "A constant float value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Vector2", fullName = "Vector2Node", description = "A constant Vector2 value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Vector3", fullName = "Vector3Node", description = "A constant Vector3 value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Vector4", fullName = "Vector4Node", description = "A constant Vector4 value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Boolean", fullName = "BooleanNode", description = "A constant boolean value" },
                new NodeTypeInfo { category = "Input/Basic", name = "Integer", fullName = "IntegerNode", description = "A constant integer value" },
                
                new NodeTypeInfo { category = "Input/Texture", name = "Sample Texture 2D", fullName = "SampleTexture2DNode", description = "Sample a 2D texture" },
                new NodeTypeInfo { category = "Input/Texture", name = "Sample Texture 3D", fullName = "SampleTexture3DNode", description = "Sample a 3D texture" },
                new NodeTypeInfo { category = "Input/Texture", name = "Sample Cubemap", fullName = "SampleCubemapNode", description = "Sample a cubemap" },
                new NodeTypeInfo { category = "Input/Texture", name = "Texture 2D Asset", fullName = "Texture2DAssetNode", description = "Reference to a 2D texture asset" },
                
                new NodeTypeInfo { category = "Input/Geometry", name = "Position", fullName = "PositionNode", description = "Vertex or fragment position" },
                new NodeTypeInfo { category = "Input/Geometry", name = "Normal", fullName = "NormalNode", description = "Surface normal vector" },
                new NodeTypeInfo { category = "Input/Geometry", name = "UV", fullName = "UVNode", description = "Texture coordinates" },
                new NodeTypeInfo { category = "Input/Geometry", name = "Vertex Color", fullName = "VertexColorNode", description = "Per-vertex color" },
                new NodeTypeInfo { category = "Input/Geometry", name = "View Direction", fullName = "ViewDirectionNode", description = "Direction from surface to camera" },
                new NodeTypeInfo { category = "Input/Geometry", name = "Tangent", fullName = "TangentNode", description = "Surface tangent vector" },
                new NodeTypeInfo { category = "Input/Geometry", name = "Bitangent", fullName = "BitangentNode", description = "Surface bitangent vector" },
                
                new NodeTypeInfo { category = "Input/Scene", name = "Time", fullName = "TimeNode", description = "Various time values" },
                new NodeTypeInfo { category = "Input/Scene", name = "Camera", fullName = "CameraNode", description = "Camera properties" },
                new NodeTypeInfo { category = "Input/Scene", name = "Screen Position", fullName = "ScreenPositionNode", description = "Screen-space position" },
                new NodeTypeInfo { category = "Input/Scene", name = "Scene Color", fullName = "SceneColorNode", description = "Sample the scene color buffer" },
                new NodeTypeInfo { category = "Input/Scene", name = "Scene Depth", fullName = "SceneDepthNode", description = "Sample the scene depth buffer" },
                
                // Math
                new NodeTypeInfo { category = "Math/Basic", name = "Add", fullName = "AddNode", description = "Add two values" },
                new NodeTypeInfo { category = "Math/Basic", name = "Subtract", fullName = "SubtractNode", description = "Subtract two values" },
                new NodeTypeInfo { category = "Math/Basic", name = "Multiply", fullName = "MultiplyNode", description = "Multiply two values" },
                new NodeTypeInfo { category = "Math/Basic", name = "Divide", fullName = "DivideNode", description = "Divide two values" },
                new NodeTypeInfo { category = "Math/Basic", name = "Power", fullName = "PowerNode", description = "Raise to a power" },
                new NodeTypeInfo { category = "Math/Basic", name = "Square Root", fullName = "SquareRootNode", description = "Square root" },
                
                new NodeTypeInfo { category = "Math/Advanced", name = "Lerp", fullName = "LerpNode", description = "Linear interpolation" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Smoothstep", fullName = "SmoothstepNode", description = "Smooth interpolation" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Clamp", fullName = "ClampNode", description = "Clamp value to range" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Saturate", fullName = "SaturateNode", description = "Clamp to 0-1" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Remap", fullName = "RemapNode", description = "Remap value from one range to another" },
                new NodeTypeInfo { category = "Math/Advanced", name = "One Minus", fullName = "OneMinusNode", description = "1 - input" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Absolute", fullName = "AbsoluteNode", description = "Absolute value" },
                new NodeTypeInfo { category = "Math/Advanced", name = "Negate", fullName = "NegateNode", description = "Negate value" },
                
                new NodeTypeInfo { category = "Math/Trigonometry", name = "Sine", fullName = "SineNode", description = "Sine function" },
                new NodeTypeInfo { category = "Math/Trigonometry", name = "Cosine", fullName = "CosineNode", description = "Cosine function" },
                new NodeTypeInfo { category = "Math/Trigonometry", name = "Tangent", fullName = "TangentNode", description = "Tangent function" },
                
                new NodeTypeInfo { category = "Math/Vector", name = "Dot Product", fullName = "DotProductNode", description = "Dot product of two vectors" },
                new NodeTypeInfo { category = "Math/Vector", name = "Cross Product", fullName = "CrossProductNode", description = "Cross product of two vectors" },
                new NodeTypeInfo { category = "Math/Vector", name = "Normalize", fullName = "NormalizeNode", description = "Normalize a vector" },
                new NodeTypeInfo { category = "Math/Vector", name = "Length", fullName = "LengthNode", description = "Length of a vector" },
                new NodeTypeInfo { category = "Math/Vector", name = "Distance", fullName = "DistanceNode", description = "Distance between two points" },
                new NodeTypeInfo { category = "Math/Vector", name = "Reflect", fullName = "ReflectNode", description = "Reflect vector" },
                
                // UV
                new NodeTypeInfo { category = "UV", name = "Tiling And Offset", fullName = "TilingAndOffsetNode", description = "Tile and offset UVs" },
                new NodeTypeInfo { category = "UV", name = "Rotate", fullName = "RotateNode", description = "Rotate UVs" },
                new NodeTypeInfo { category = "UV", name = "Polar Coordinates", fullName = "PolarCoordinatesNode", description = "Convert to polar coordinates" },
                new NodeTypeInfo { category = "UV", name = "Radial Shear", fullName = "RadialShearNode", description = "Radial shear distortion" },
                new NodeTypeInfo { category = "UV", name = "Spherize", fullName = "SpherizeNode", description = "Spherical distortion" },
                new NodeTypeInfo { category = "UV", name = "Twirl", fullName = "TwirlNode", description = "Twirl distortion" },
                
                // Artistic
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "Contrast", fullName = "ContrastNode", description = "Adjust contrast" },
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "Saturation", fullName = "SaturationNode", description = "Adjust saturation" },
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "Hue", fullName = "HueNode", description = "Shift hue" },
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "Invert Colors", fullName = "InvertColorsNode", description = "Invert colors" },
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "Replace Color", fullName = "ReplaceColorNode", description = "Replace a color" },
                new NodeTypeInfo { category = "Artistic/Adjustment", name = "White Balance", fullName = "WhiteBalanceNode", description = "Adjust white balance" },
                
                new NodeTypeInfo { category = "Artistic/Blend", name = "Blend", fullName = "BlendNode", description = "Blend two colors" },
                
                new NodeTypeInfo { category = "Artistic/Filter", name = "Blur", fullName = "BlurNode", description = "Blur effect" },
                
                new NodeTypeInfo { category = "Artistic/Normal", name = "Normal Strength", fullName = "NormalStrengthNode", description = "Adjust normal map strength" },
                new NodeTypeInfo { category = "Artistic/Normal", name = "Normal Blend", fullName = "NormalBlendNode", description = "Blend two normal maps" },
                new NodeTypeInfo { category = "Artistic/Normal", name = "Normal From Height", fullName = "NormalFromHeightNode", description = "Generate normal from height" },
                new NodeTypeInfo { category = "Artistic/Normal", name = "Normal Unpack", fullName = "NormalUnpackNode", description = "Unpack normal map" },
                
                // Channel
                new NodeTypeInfo { category = "Channel", name = "Split", fullName = "SplitNode", description = "Split vector into components" },
                new NodeTypeInfo { category = "Channel", name = "Combine", fullName = "CombineNode", description = "Combine components into vector" },
                new NodeTypeInfo { category = "Channel", name = "Swizzle", fullName = "SwizzleNode", description = "Reorder vector components" },
                
                // Procedural
                new NodeTypeInfo { category = "Procedural/Noise", name = "Simple Noise", fullName = "SimpleNoiseNode", description = "Simple noise pattern" },
                new NodeTypeInfo { category = "Procedural/Noise", name = "Gradient Noise", fullName = "GradientNoiseNode", description = "Gradient/Perlin noise" },
                new NodeTypeInfo { category = "Procedural/Noise", name = "Voronoi", fullName = "VoronoiNode", description = "Voronoi/cellular noise" },
                
                new NodeTypeInfo { category = "Procedural/Shape", name = "Ellipse", fullName = "EllipseNode", description = "Ellipse shape" },
                new NodeTypeInfo { category = "Procedural/Shape", name = "Rectangle", fullName = "RectangleNode", description = "Rectangle shape" },
                new NodeTypeInfo { category = "Procedural/Shape", name = "Rounded Rectangle", fullName = "RoundedRectangleNode", description = "Rounded rectangle shape" },
                new NodeTypeInfo { category = "Procedural/Shape", name = "Polygon", fullName = "PolygonNode", description = "Regular polygon shape" },
                
                new NodeTypeInfo { category = "Procedural", name = "Checkerboard", fullName = "CheckerboardNode", description = "Checkerboard pattern" },
                
                // Utility
                new NodeTypeInfo { category = "Utility", name = "Preview", fullName = "PreviewNode", description = "Preview intermediate result" },
                new NodeTypeInfo { category = "Utility", name = "Custom Function", fullName = "CustomFunctionNode", description = "Custom HLSL function" },
                new NodeTypeInfo { category = "Utility", name = "Sub Graph", fullName = "SubGraphNode", description = "Reference a sub-graph" },
                new NodeTypeInfo { category = "Utility", name = "Keyword", fullName = "KeywordNode", description = "Shader keyword branch" },
                new NodeTypeInfo { category = "Utility", name = "Branch", fullName = "BranchNode", description = "Conditional branch" },
                new NodeTypeInfo { category = "Utility", name = "Comparison", fullName = "ComparisonNode", description = "Compare two values" },
            };

            return RpcResponse.Success(new ListShaderGraphNodeTypesResponse
            {
                nodeTypes = nodeTypes,
                totalCount = nodeTypes.Count
            });
        }

        #endregion

        #region Helper Methods

        private static string DetectGraphType(JObject graphData)
        {
            // Check for active targets
            var activeTargets = graphData["m_ActiveTargets"] as JArray;
            if (activeTargets != null && activeTargets.Count > 0)
            {
                var targetJson = activeTargets[0]["JSONnodeData"]?.ToString();
                if (!string.IsNullOrEmpty(targetJson))
                {
                    if (targetJson.Contains("UniversalTarget")) return "URP";
                    if (targetJson.Contains("HDTarget")) return "HDRP";
                    if (targetJson.Contains("BuiltInTarget")) return "Built-in";
                }
            }

            // Check for master node type (older format)
            var nodes = graphData["m_SerializedNodes"] as JArray;
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    var nodeJson = node["JSONnodeData"]?.ToString() ?? "";
                    if (nodeJson.Contains("PBRMasterNode")) return "PBR";
                    if (nodeJson.Contains("UnlitMasterNode")) return "Unlit";
                    if (nodeJson.Contains("SpriteLitMasterNode")) return "SpriteLit";
                    if (nodeJson.Contains("SpriteUnlitMasterNode")) return "SpriteUnlit";
                }
            }

            return "Unknown";
        }

        private static string GenerateShaderGraphTemplate(string template, string name, List<PropertyDefinition> customProperties)
        {
            // Generate a unique GUID for the graph
            var graphGuid = Guid.NewGuid().ToString("N");
            
            switch (template.ToLower())
            {
                case "pbr":
                case "lit":
                    return GeneratePBRTemplate(name, graphGuid, customProperties);
                case "unlit":
                    return GenerateUnlitTemplate(name, graphGuid, customProperties);
                case "sprite":
                case "spriteunlit":
                    return GenerateSpriteUnlitTemplate(name, graphGuid, customProperties);
                default:
                    return null;
            }
        }

        private static string GeneratePBRTemplate(string name, string graphGuid, List<PropertyDefinition> customProperties)
        {
            // Minimal PBR ShaderGraph template for URP
            var template = @"{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """ + graphGuid + @""",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 0.0 },
        ""m_Blocks"": []
    },
    ""m_FragmentContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 200.0 },
        ""m_Blocks"": []
    },
    ""m_PreviewData"": {
        ""serializedMesh"": { ""m_SerializedMesh"": """", ""m_Guid"": """" },
        ""preventRotation"": false
    },
    ""m_Path"": ""Shader Graphs"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": { ""m_Id"": """" },
    ""m_SubDatas"": [],
    ""m_ActiveTargets"": []
}";
            return template;
        }

        private static string GenerateUnlitTemplate(string name, string graphGuid, List<PropertyDefinition> customProperties)
        {
            var template = @"{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """ + graphGuid + @""",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 0.0 },
        ""m_Blocks"": []
    },
    ""m_FragmentContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 200.0 },
        ""m_Blocks"": []
    },
    ""m_PreviewData"": {
        ""serializedMesh"": { ""m_SerializedMesh"": """", ""m_Guid"": """" },
        ""preventRotation"": false
    },
    ""m_Path"": ""Shader Graphs"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": { ""m_Id"": """" },
    ""m_SubDatas"": [],
    ""m_ActiveTargets"": []
}";
            return template;
        }

        private static string GenerateSpriteUnlitTemplate(string name, string graphGuid, List<PropertyDefinition> customProperties)
        {
            var template = @"{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """ + graphGuid + @""",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 0.0 },
        ""m_Blocks"": []
    },
    ""m_FragmentContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 200.0 },
        ""m_Blocks"": []
    },
    ""m_PreviewData"": {
        ""serializedMesh"": { ""m_SerializedMesh"": """", ""m_Guid"": """" },
        ""preventRotation"": false
    },
    ""m_Path"": ""Shader Graphs"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": { ""m_Id"": """" },
    ""m_SubDatas"": [],
    ""m_ActiveTargets"": []
}";
            return template;
        }

        #endregion
    }
}
