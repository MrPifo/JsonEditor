#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;

public class JsonEditor : EditorWindow {

    private GUIStyle inputStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle pathStyle;
    private GUIStyle foldoutStyle;
    private GUIStyle fieldStyle;
    private GUILayoutOption[] fieldLayout;

    private Type type;
    private JObject jsonContext;
    private TextAsset textAsset;
    private Dictionary<string, (FieldInfo, object)> unityObjects;
    private Dictionary<string, bool> labels;
    private List<FieldInfo> fields;
    private string newFilename = "";
    private string selectedDatatype = "";
    private bool initialized = false;
    private string previousLabelType = "";

    [MenuItem("Window/JsonEditor")]
    public static void ShowWindow() {
        GetWindow(typeof(JsonEditor));
    }

	public void OnEnable() {
        Selection.selectionChanged += Initialize;
    }

	public void OnGUI() {
        inputStyle = new GUIStyle("TextField") {
            fontSize = 14,
            fixedHeight = 25,
            alignment = TextAnchor.MiddleLeft,
        };
        buttonStyle = new GUIStyle("Button") {
            fontSize = 16
        };
        pathStyle = new GUIStyle("Label") {
            fontSize = 12,
            fixedHeight = 30,
        };
        labelStyle = new GUIStyle("Label") {
            fontSize = 25,
            fixedHeight = 30,
        };
        foldoutStyle = new GUIStyle("Foldout") {
            fontSize = 16,
        };
        fieldStyle = new GUIStyle("Label") {
            fontSize = 14,
            fixedHeight = 25,
        };
        fieldLayout = new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.MinWidth(Screen.width / 2),
        };
        RenderWindow();
    }

    public void RenderWindow() {
        if(initialized && type != null && fields != null && textAsset != null) {
            GUILayout.Label(type.Name, EditorStyles.boldLabel);

            GUILayout.BeginArea(new Rect(25, 100, Screen.width - 50, Screen.height));
            GUILayout.Label(type.Name + " - " + textAsset.name, labelStyle);
            GUILayout.Space(10);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(25);
            

            ProcessFields();
            GUILayout.Space(50);
            PrintDatatypeSelector();
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();
            labelStyle.fontSize = 20;

            if(GUILayout.Button("Save", buttonStyle, GUILayout.Height(30), GUILayout.Width(150)))
                SaveJson();
            if(GUILayout.Button("Create New", buttonStyle, GUILayout.Height(30), GUILayout.Width(150)))
                CreateJson();

            GUILayout.EndHorizontal();
            GUILayout.Space(25);


            // "target" can be any class derrived from ScriptableObject 
            // (could be EditorWindow, MonoBehaviour, etc)
            /*ScriptableObject target = this;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty stringsProperty = so.FindProperty("Strings");

            EditorGUILayout.PropertyField(stringsProperty, true); // True means show children
            so.ApplyModifiedProperties(); // Remember to apply modified properties*/


            GUILayout.EndArea();
        } else {
            GUILayout.Label("Json Editor", EditorStyles.boldLabel);
            GUILayout.BeginArea(new Rect(25, 100, Screen.width - 50, Screen.height));
            GUILayout.Label("Json Editor", labelStyle);
            GUILayout.Space(25);
            GUILayout.BeginVertical();
            PrintDatatypeSelector();
            GUILayout.Space(25);
            if(GUILayout.Button("Create New", buttonStyle, GUILayout.Height(30), GUILayout.Width(110)))
                CreateJson();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

	private void Update() {
        if(initialized) {
            Repaint();
        }
	}

	public void Initialize() {
        initialized = false;

        // Initialize after a TextAsset file has been selected
        if(Selection.activeObject && Selection.activeObject.GetType() == typeof(TextAsset)) {
            // Find compatible DataType
            try {
                textAsset = (TextAsset)Selection.activeObject;
                jsonContext = JObject.Parse(textAsset.text);
                unityObjects = new Dictionary<string, (FieldInfo, object)>();
                if(labels == null) {
                    labels = new Dictionary<string, bool>();
                }
            } catch(Exception e) {
                Debug.LogError("Failed parsing the json file! \n " + e);
                jsonContext = null;
			}

            // Continue if parsing succeeded
            if(jsonContext != null && !string.IsNullOrEmpty(selectedDatatype)) {
                // Setup Selected Datatype
                type = Type.GetType(selectedDatatype);

                if(type != null) {
                    fields = new List<FieldInfo>(type.GetFields());

                    fields = fields.OrderBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.Int16 || Type.GetTypeCode(g.FieldType) == TypeCode.Int32 || Type.GetTypeCode(g.FieldType) == TypeCode.Int64)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.UInt16 || Type.GetTypeCode(g.FieldType) == TypeCode.UInt32 || Type.GetTypeCode(g.FieldType) == TypeCode.UInt64)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.Single || Type.GetTypeCode(g.FieldType) == TypeCode.Double || Type.GetTypeCode(g.FieldType) == TypeCode.Decimal)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.String || Type.GetTypeCode(g.FieldType) == TypeCode.Char || Type.GetTypeCode(g.FieldType) == TypeCode.DateTime)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.Boolean)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.Object)
                    .ThenBy(g => Type.GetTypeCode(g.FieldType) == TypeCode.Byte || Type.GetTypeCode(g.FieldType) == TypeCode.SByte)
                    .ThenBy(g => g.FieldType.IsEnum)
                    .ToList();

                    initialized = true;
                    LoadReferences();
                } else {
                    // Datatype not found

				}
            }
        }
    }

    public void ProcessFields() {
        GUILayout.BeginVertical();

        previousLabelType = "";
        foreach(var field in fields) {
            try {
                if(jsonContext.TryGetValue(field.Name, out JToken token) && !field.Name.EndsWith("_path") && !field.FieldType.IsEnum) {
                    switch(Type.GetTypeCode(field.FieldType)) {
                        case TypeCode.Boolean:
                            PrintHeader("Boolean");

                            if(labels["Boolean"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = EditorGUILayout.Toggle(token.Value<bool>(), fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "Boolean";
                            break;
                        case TypeCode.Double:
                            PrintHeader("Double");

                            if(labels["Double"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = EditorGUILayout.DoubleField(token.Value<double>(), inputStyle, fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "Double";
                            break;
                        case TypeCode.Decimal:
                            PrintHeader("Decimal");

                            if(labels["Decimal"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = (decimal)EditorGUILayout.DoubleField(token.Value<double>(), inputStyle, fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "Decimal";
                            break;
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            PrintHeader("Integer");

                            if(labels["Integer"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = EditorGUILayout.IntField(token.Value<int>(), inputStyle, fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "Integer";
                            break;
                        case TypeCode.Single:
                            PrintHeader("Float");

                            if(labels["Float"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = EditorGUILayout.FloatField(token.Value<float>(), inputStyle, fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "Float";
                            break;
                        case TypeCode.String:
                        case TypeCode.Char:
                        case TypeCode.Byte:
                        case TypeCode.SByte:
                        case TypeCode.DateTime:
                            PrintHeader("String");

                            if(labels["String"]) {
                                GUILayout.BeginHorizontal();
                                PrintFieldLabel(field.Name);
                                jsonContext[field.Name] = EditorGUILayout.TextField(token.Value<string>(), inputStyle, fieldLayout);
                                GUILayout.EndHorizontal();
                            }
                            previousLabelType = "String";
                            break;
                        default:
                            ProcessOtherDatatype(field, fieldLayout);
                            break;
                    }
                } else {
                    ProcessOtherDatatype(field, fieldLayout);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            } catch(Exception e) {
                EditorGUILayout.EndFoldoutHeaderGroup();
                GUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Parsing error.", MessageType.Error);
                Debug.LogWarning("Parsing Error! \n " + e.StackTrace);
            }
		}
        GUILayout.EndVertical();
    }

    private void ProcessOtherDatatype(FieldInfo field, GUILayoutOption[] fieldLayout) {
        // Processes Unity Datatypes
        if(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) {
            // Process if Field is Object Reference -> An extra field "FieldName" + "_path" is required for every object reference
            // Example: 
            // GameObject myObject;     => Cannot be serialized
            // string myObject_path;    => Stores the path to the asset
            PrintHeader("References");

            if(labels["References"]) {
                if(!unityObjects.ContainsKey(field.Name))
                    unityObjects.Add(field.Name, (field, null));

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                PrintFieldLabel(field.Name);
                GUILayout.Space(20);
                if(typeof(UnityEngine.GameObject).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(field.FieldType)) {
                    unityObjects[field.Name] = (field, EditorGUILayout.ObjectField((UnityEngine.GameObject)unityObjects[field.Name].Item2, typeof(UnityEngine.GameObject), false, fieldLayout));
                } else if(typeof(UnityEngine.Texture).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Sprite).IsAssignableFrom(field.FieldType)) {
                    unityObjects[field.Name] = (field, EditorGUILayout.ObjectField((UnityEngine.Texture)unityObjects[field.Name].Item2, typeof(UnityEngine.Texture), false, fieldLayout));
                } else {
                    unityObjects[field.Name] = (field, EditorGUILayout.ObjectField((UnityEngine.Object)unityObjects[field.Name].Item2, typeof(UnityEngine.Object), false, fieldLayout));
                }
                GUILayout.EndHorizontal();
                if(unityObjects[field.Name].Item2 != null) {
                    ConvertReferenceToPath(field.Name, unityObjects[field.Name]);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(jsonContext[field.Name + "_path"].ToString(), pathStyle, fieldLayout);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(10);
                }
                GUILayout.EndVertical();
            }
            previousLabelType = "References";
        } else if(field.FieldType.IsEnum) {
            // Process if Field is an Enum
            // Must be saved as an Integer
            PrintHeader("Enum");

            if(labels["Enum"]) {
                GUILayout.BeginHorizontal();
                PrintFieldLabel(field.Name);
                GUILayout.Space(20);
                int[] enValues = new int[Enum.GetValues(field.FieldType).Length];
                for(int i = 0; i < enValues.Length; i++) {
                    enValues[i] = (int)Enum.GetValues(field.FieldType).GetValue(i);
                }
                jsonContext[field.Name] = EditorGUILayout.IntPopup(jsonContext[field.Name].Value<int>(), Enum.GetNames(field.FieldType), enValues);
                GUILayout.EndHorizontal();
            }
            previousLabelType = "Color";
        } else if(unityObjects.ContainsKey(field.Name) && (typeof(UnityEngine.Color).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Color32).IsAssignableFrom(field.FieldType))) {
            // Process if Field is Color/Color32
            // RGB and RGBA are both supported
            // Objects with 3 parameters are supported for serializing
            // Example:
            // [Serializable]
            // class CustomColor {
            //     int value1 = 255;
            //     int value2 = 255;
            //     int value3 = 255;
            // }
            // OR THIS
            // [Serializable]
            // class CustomColorRGBA {
            //     int r = 255;
            //     int g = 255;
            //     int b = 255;
            //     int a = 255;
            // }

            PrintHeader("Color");
            if(labels["Color"]) {
                GUILayout.BeginHorizontal();
                PrintFieldLabel(field.Name);

                // Check Compability with Datatype
                JHelper col = new JHelper();
                if(unityObjects[field.Name].Item2 != null) {
                    col = (JHelper)unityObjects[field.Name].Item2;
                    var color = (UnityEngine.Color)col.obj;
                    JObject j = new JObject {
                        { col.fields[0], color.r },
                        { col.fields[1], color.g },
                        { col.fields[2], color.b }
                    };
                    if(col.fields[3] != null) {
                        j.Add(col.fields[3], color.a);
                    }
                    jsonContext[field.Name] = j;
                }

                GUILayout.Space(20);
                unityObjects[field.Name] = (field, new JHelper(EditorGUILayout.ColorField((Color)col.obj, fieldLayout), new List<string>(col.fields).ToArray()));
                GUILayout.EndHorizontal();
            }
            previousLabelType = "Enum";
        } else if(unityObjects.ContainsKey(field.Name) && (typeof(UnityEngine.Vector3).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Vector3Int).IsAssignableFrom(field.FieldType))) {
            // Process if Field is Vector3 / Vector3Int

            PrintHeader("Vector3");
            if(labels["Vector3"]) {
                GUILayout.BeginHorizontal();
                PrintFieldLabel(field.Name);
                GUILayout.Space(20);

                // Check Compability with Datatype
                if(unityObjects[field.Name].Item2 != null) {
                    var helper = (JHelper)unityObjects[field.Name].Item2;
                    if(helper.obj.GetType() == typeof(UnityEngine.Vector3)) {
                        // Input for Vector3
                        var vec3 = (UnityEngine.Vector3)helper.obj;
                        unityObjects[field.Name] = (field, new JHelper(EditorGUILayout.Vector3Field("", vec3), helper.fields));
                        JObject j = new JObject {
                            { helper.fields[0], vec3.x },
                            { helper.fields[1], vec3.y },
                            { helper.fields[2], vec3.z }
                        };
                        jsonContext[field.Name] = j;
                    } else {
                        // Input for Vector3Int
                        var vec3Int = (UnityEngine.Vector3Int)helper.obj;
                        unityObjects[field.Name] = (field, new JHelper(EditorGUILayout.Vector3Field("", vec3Int), helper.fields));
                        JObject j = new JObject {
                            { helper.fields[0], vec3Int.x },
                            { helper.fields[1], vec3Int.y },
                            { helper.fields[2], vec3Int.z }
                        };
                        jsonContext[field.Name] = j;
                    }
                }
                GUILayout.EndHorizontal();
            }
            previousLabelType = "Vector3";
        } else if(unityObjects.ContainsKey(field.Name) && (typeof(UnityEngine.Vector2).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Vector2Int).IsAssignableFrom(field.FieldType))) {
            // Process if Field is Vector2 / Vector2Int

            PrintHeader("Vector2");
            if(labels["Vector2"]) {
                GUILayout.BeginHorizontal();
                PrintFieldLabel(field.Name);
                GUILayout.Space(20);

                // Check Compability with Datatype
                if(unityObjects[field.Name].Item2 != null) {
                    var helper = (JHelper)unityObjects[field.Name].Item2;
                    if(helper.obj.GetType() == typeof(UnityEngine.Vector2)) {
                        // Input for Vector2
                        var vec2 = (UnityEngine.Vector2)helper.obj;
                        unityObjects[field.Name] = (field, new JHelper(EditorGUILayout.Vector2Field("", vec2), helper.fields));
                        JObject j = new JObject {
                            { helper.fields[0], vec2.x },
                            { helper.fields[1], vec2.y }
                        };
                        jsonContext[field.Name] = j;
                    } else {
                        // Input for Vector2Int
                        var vec2Int = (UnityEngine.Vector2Int)helper.obj;
                        unityObjects[field.Name] = (field, new JHelper(EditorGUILayout.Vector2Field("", vec2Int), helper.fields));
                        JObject j = new JObject {
                            { helper.fields[0], vec2Int.x },
                            { helper.fields[1], vec2Int.y }
                        };
                        jsonContext[field.Name] = j;
                    }
                }
                GUILayout.EndHorizontal();
            }
            previousLabelType = "Vector2";
        }
    }

    private void PrintHeader(string title) {
        if(title != previousLabelType) {
            if(!labels.ContainsKey(title)) {
                GUILayout.Space(25);
                bool isFolded = false;
                if(labels.ContainsKey(title))
                    isFolded = true;
                
                labels.Add(title, EditorGUILayout.BeginFoldoutHeaderGroup(isFolded, title, foldoutStyle));
                GUILayout.Space(10);
            } else {
                labels[title] = EditorGUILayout.BeginFoldoutHeaderGroup(labels[title], title, foldoutStyle);
            }
            GUILayout.Space(10);
        }
    }

    private void PrintFieldLabel(string name) {
        GUILayout.Space(20);
        GUILayout.Label(name, fieldStyle, GUILayout.ExpandWidth(false), GUILayout.MinWidth(100));
    }

    private void PrintDatatypeSelector() {
        labelStyle.fontSize = 16;
        GUILayout.Label("", GUI.skin.horizontalSlider);
        GUILayout.Space(25);
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Datatype", labelStyle);
        selectedDatatype = GUILayout.TextField(selectedDatatype, inputStyle, GUILayout.MinWidth(150));
        GUILayout.EndHorizontal();
        type = Type.GetType(selectedDatatype);
        if(type == null) {
            EditorGUILayout.HelpBox("No valid Datatype found.", MessageType.Warning, true);
		}
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filename: ", labelStyle);
        newFilename = GUILayout.TextField(newFilename, inputStyle, GUILayout.MinWidth(150));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();


    }

    private void ConvertReferenceToPath(string name, (FieldInfo, object) source) {
        if(source.Item2 != null) {
            var o = (UnityEngine.Object)source.Item2;
            unityObjects[name] = (unityObjects[name].Item1, o);
            jsonContext[name + "_path"] = FetchPath(o).Replace("Assets/", "");
        }
    }

    private T ConvertPathToReference<T>(string path) where T: UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>("Assets/" + path);

    public static string ReplaceFirstOccurrence(string Source, string Find, string Replace) {
        int Place = Source.IndexOf(Find);
        string result = Source.Remove(Place, Find.Length).Insert(Place, Replace);
        return result;
    }

    public static string ReplaceLastOccurrence(string Source, string Find, string Replace) {
        int place = Source.LastIndexOf(Find);

        if(place == -1)
            return Source;

        string result = Source.Remove(place, Find.Length).Insert(place, Replace);
        return result;
    }

    private string FetchPath(UnityEngine.Object o) => AssetDatabase.GetAssetPath(o);

    private void LoadReferences() {
        foreach(var field in type.GetFields()) {
            if(jsonContext.TryGetValue(field.Name, out JToken token)) {
                if(field.Name.EndsWith("_path")) {
                    // Load and Fetch Object References
                    var asset = ConvertPathToReference<UnityEngine.Object>(token.Value<string>());
                    string targetField = ReplaceLastOccurrence(field.Name, "_path", "");

                    if(!unityObjects.ContainsKey(targetField))
                        unityObjects.Add(targetField, (fields.Where(f => f.Name == targetField).First(), null));

                    if(asset != null && unityObjects.TryGetValue(targetField, out (FieldInfo, object) value)) {
                        if(value.Item1.FieldType == asset.GetType() || asset.GetType() == typeof(GameObject)) {
                            unityObjects[targetField] = (value.Item1, asset);
                        } else if(asset.GetType() == typeof(UnityEngine.Texture2D)) {
                            Texture2D tex = (Texture2D)asset;
                            unityObjects[targetField] = (value.Item1, tex);
                        } else if(asset != null) {
                            Debug.LogError("Loaded asset " + asset.name + " doesn't match the given type!");
                        }
                    }
                } else if(typeof(UnityEngine.Color).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Color32).IsAssignableFrom(field.FieldType)) {
                    // Load and Parse Color value
                    if(!unityObjects.ContainsKey(field.Name))
                        unityObjects.Add(field.Name, (field, null));

                    JHelper color = new JHelper();
                    try {
                        if(token.HasValues) {
                            float[] rgba = new float[4] { 0, 0, 0, 0 };
                            bool isColor32 = false;
                            float alpha = 1;
                            int i = 0;

                            // Loop object and find 3/4 values to parse to Color
                            foreach(var j in token.Values<JProperty>()) {
                                float num = 0;
                                if(int.TryParse(j.Value.ToString(), out int numInt)) {
                                    num = numInt;
                                } else if(float.TryParse(j.Value.ToString(), out num))

                                    if(!isColor32 && num > 1) {
                                        isColor32 = true;
                                    }

                                switch(i) {
                                    case 0:
                                        rgba[0] = num;
                                        break;
                                    case 1:
                                        rgba[1] = num;
                                        break;
                                    case 2:
                                        rgba[2] = num;
                                        break;
                                    case 3:
                                        rgba[3] = num;
                                        break;
                                }
                                color.fields[i] = j.Name;
                                i++;
                            }
                            if(i < 3) {
                                if(isColor32)
                                    alpha = 255;
                                else
                                    alpha = 1;
                            } else {
                                alpha = rgba[3];
                            }
                            if(isColor32) {
                                color.obj = new Color32((byte)rgba[0], (byte)rgba[1], (byte)rgba[2], (byte)alpha);
                            } else {
                                color.obj = new Color(rgba[0], rgba[1], rgba[2], alpha);
                            }

                            unityObjects[field.Name] = (field, color);
                        }
                    } catch(IndexOutOfRangeException) {
                        Debug.LogError("Color index out of range!");
                    } catch {
                        Debug.LogError("Failed parsing Color!");
                    }
                } else if((typeof(UnityEngine.Vector3).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Vector3Int).IsAssignableFrom(field.FieldType))) {
                    if(!unityObjects.ContainsKey(field.Name))
                        unityObjects.Add(field.Name, (field, Vector3Int.zero));

                    Vector3 vec = new Vector3();
                    Vector3Int vecInt = new Vector3Int();
                    bool isInt = false;
                    if((typeof(UnityEngine.Vector3Int).IsAssignableFrom(field.FieldType))) {
                        isInt = true;
					}

                    try {
                        if(token.HasValues) {
                            var fields = new List<string>();
                            int i = 0;
                            foreach(var j in token.Values<JProperty>()) {
                                float num = 0;
                                if(isInt && float.TryParse(j.Value.ToString(), out float numInt)) {
                                    switch(i) {
                                        case 0:
                                            vecInt.x = (int)numInt;
                                            break;
                                        case 1:
                                            vecInt.y = (int)numInt;
                                            break;
                                        case 2:
                                            vecInt.z = (int)numInt;
                                            break;
                                    }
                                } else if(float.TryParse(j.Value.ToString(), out num)) {
                                    switch(i) {
                                        case 0:
                                            vec.x = num;
                                            break;
                                        case 1:
                                            vec.y = num;
                                            break;
                                        case 2:
                                            vec.z = num;
                                            break;
                                    }
                                }
                                fields.Add(j.Name);
                                i++;
                            }
                            
                            if(isInt)
                                unityObjects[field.Name] = (field, new JHelper(vecInt, fields.ToArray()));
                            else
                                unityObjects[field.Name] = (field, new JHelper(vec, fields.ToArray()));
                        }
                    } catch(Exception e) {
                        Debug.LogError("Failed parsing Vector3! \n " + e);
                    }
                } else if((typeof(UnityEngine.Vector2).IsAssignableFrom(field.FieldType) || typeof(UnityEngine.Vector2Int).IsAssignableFrom(field.FieldType))) {
                    if(!unityObjects.ContainsKey(field.Name))
                        unityObjects.Add(field.Name, (field, Vector2Int.zero));

                    Vector2 vec = new Vector2();
                    Vector2Int vecInt = new Vector2Int();
                    bool isInt = false;
                    if((typeof(UnityEngine.Vector2Int).IsAssignableFrom(field.FieldType))) {
                        isInt = true;
                    }

                    try {
                        if(token.HasValues) {
                            var fields = new List<string>();
                            int i = 0;
                            foreach(var j in token.Values<JProperty>()) {
                                float num = 0;
                                if(isInt && float.TryParse(j.Value.ToString(), out float numInt)) {
                                    switch(i) {
                                        case 0:
                                            vecInt.x = (int)numInt;
                                            break;
                                        case 1:
                                            vecInt.y = (int)numInt;
                                            break;
                                    }
                                } else if(float.TryParse(j.Value.ToString(), out num)) {
                                    switch(i) {
                                        case 0:
                                            vec.x = num;
                                            break;
                                        case 1:
                                            vec.y = num;
                                            break;
                                    }
                                }
                                fields.Add(j.Name);
                                i++;
                            }
                            if(isInt)
                                unityObjects[field.Name] = (field, new JHelper(vecInt, fields.ToArray()));
                            else
                                unityObjects[field.Name] = (field, new JHelper(vec, fields.ToArray()));
                        }
                    } catch {
                        Debug.LogError("Failed parsing Vector2!");
                    }
                }
            }
        }
    }

    public void SaveJson() {
        string path = (Application.dataPath + AssetDatabase.GetAssetPath(textAsset)).Replace("AssetsAssets", "Assets");

        try {
            File.WriteAllText(path, jsonContext.ToString().Trim());
            AssetDatabase.Refresh();
            Debug.Log(textAsset.name + " saved!");
        } catch {
            Debug.LogError("Failed writing .json to " + path);
		}
	}

    public void CreateJson() {
        try {
            Type type = Type.GetType(selectedDatatype);
            jsonContext = JObject.FromObject(Activator.CreateInstance(type));
            string path = Application.dataPath + "/" + newFilename + ".json";
            if(textAsset != null) {
                path = (Application.dataPath + AssetDatabase.GetAssetPath(textAsset)).Replace("AssetsAssets", "");
                path = Path.GetDirectoryName(path) + "/" + newFilename + ".json";
            }
            Debug.Log(path);

            File.WriteAllText(path, jsonContext.ToString().Trim());
            AssetDatabase.Refresh();
        } catch (Exception e) {
            Debug.LogError("Failed creating new file. \n" + e);
		}
	}

    private class JHelper {
        public object obj;
        public string[] fields;

        public JHelper() {
            obj = null;
            fields = new string[4];
        }
        public JHelper(object o) {
            obj = o;
            fields = new string[4];
        }
        public JHelper(object o, string[] _fields) {
            obj = o;
            fields = _fields;
        }
    }
}
#endif