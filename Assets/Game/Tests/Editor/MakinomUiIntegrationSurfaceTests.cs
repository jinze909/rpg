using System;
using System.Linq;
using System.Reflection;
using GamingIsLove.Makinom;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public class MakinomUiIntegrationSurfaceTests
    {
        private const string ProjectPath = "Assets/Gaming Is Love/_Data/Project.asset";
        private const string UiSettingsPath = "Assets/Gaming Is Love/_Data/_Settings/UI System Settings.asset";

        [Test, Order(5)]
        public void UnityUiModuleExposesItsSystemSettingsAndCreationCommands()
        {
            var runtimeAssembly = Assembly.Load("Makinom2.UnityUI");
            var editorAssembly = Assembly.Load("Makinom2Editor.UnityUI");
            var unityUiSystem = runtimeAssembly.GetType(
                "GamingIsLove.Makinom.UI.UnityUISystem", throwOnError: true);

            Assert.That(unityUiSystem.IsAbstract, Is.False,
                "The installed Makinom Unity UI module must expose a concrete UnityUISystem.");
            DumpType("UnityUISystem", unityUiSystem, includeAllMembers: true);

            var project = AssetDatabase.LoadAssetAtPath<MakinomProjectAsset>(ProjectPath);
            Assert.That(project, Is.Not.Null, "The saved Makinom project is required.");
            if (!Maki.Initialized)
            {
                Maki.Initialize(project);
            }

            Assert.That(Maki.Initialized, Is.True);
            Assert.That(Maki.Data.ProjectAsset, Is.SameAs(project));
            DumpType("Maki", typeof(Maki));
            DumpType("Maki.Data", Maki.Data.GetType());
            DumpType("MakinomProjectAsset", project.GetType());

            var uiSystemSettings = Maki.UISystem;
            Assert.That(uiSystemSettings, Is.Not.Null,
                "Makinom must load its saved UI System Settings object.");
            DumpType("Maki.UISystem", uiSystemSettings.GetType(), includeAllMembers: true);
            DumpCurrentSystem(uiSystemSettings);

            Assert.That(Maki.UIBoxes, Is.Not.Null);
            DumpType("Maki.UIBoxes", Maki.UIBoxes.GetType(), includeAllMembers: true);
            Assert.That(Maki.UISettings, Is.Not.Null);
            DumpType("Maki.UISettings", Maki.UISettings.GetType(), includeAllMembers: true);
            Assert.That(Maki.UILayers, Is.Not.Null);
            DumpType("Maki.UILayers", Maki.UILayers.GetType(), includeAllMembers: true);
            Assert.That(Maki.UILayouts, Is.Not.Null);
            DumpType("Maki.UILayouts", Maki.UILayouts.GetType(), includeAllMembers: true);

            var uiSettingsAsset = AssetDatabase.LoadMainAssetAtPath(UiSettingsPath);
            Assert.That(uiSettingsAsset, Is.Not.Null,
                "The saved UI System Settings asset must exist before configuring Unity UI.");
            DumpType("UI System Settings asset", uiSettingsAsset.GetType());
            var dataField = FindField(uiSettingsAsset.GetType(), "data");
            Assert.That(dataField, Is.Not.Null, "The settings asset must expose its serialized data.");
            var data = dataField.GetValue(uiSettingsAsset);
            Assert.That(data, Is.Not.Null);
            DumpType("UI System Settings data", data.GetType(), includeAllMembers: true);

            var menuItems = FindMenuItems(runtimeAssembly)
                .Concat(FindMenuItems(editorAssembly))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
            foreach (var item in menuItems.Where(x =>
                x.IndexOf("Makinom/UI/Canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.IndexOf("UI Box Scroll Vertical with Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.IndexOf("Choice Info Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.IndexOf("Tab Button (TextMeshPro)", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Debug.Log("RPG_ORK_UI_MENU: " + item);
            }

            Assert.That(menuItems.Any(x =>
                x.IndexOf("Makinom/UI/Canvas", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, "The Unity UI module must provide its Canvas creation command.");
            Assert.That(menuItems.Any(x =>
                x.IndexOf("UI Box Scroll Vertical with Title", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, "The Unity UI module must provide a default scrollable UI Box command.");
            Assert.That(menuItems.Any(x =>
                x.IndexOf("Choice Info Button", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, "The Unity UI module must provide a choice button command.");

            Debug.Log("RPG_ORK_UI_SURFACE_VALIDATED");
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string[] FindMenuItems(Assembly assembly)
        {
            var results = new System.Collections.Generic.List<string>();
            foreach (var type in SafeGetTypes(assembly))
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    foreach (var attribute in CustomAttributeData.GetCustomAttributes(method))
                    {
                        if (attribute.AttributeType.FullName != "UnityEditor.MenuItem" ||
                            attribute.ConstructorArguments.Count == 0)
                            continue;
                        var path = attribute.ConstructorArguments[0].Value as string;
                        if (!string.IsNullOrEmpty(path)) results.Add(path);
                    }
                }
            }
            return results.ToArray();
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types.Where(type => type != null).ToArray();
            }
        }

        private static void DumpCurrentSystem(object settings)
        {
            var type = settings.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length != 0 ||
                    (!property.Name.Contains("System") && !property.PropertyType.Name.Contains("UISystem")))
                    continue;
                try
                {
                    var value = property.GetValue(settings);
                    Debug.Log("RPG_ORK_UI_CURRENT: " + property.Name + " = " +
                        (value == null ? "<null>" : value.GetType().FullName));
                    if (value != null) DumpType("Maki.UISystem." + property.Name, value.GetType(), true);
                }
                catch (Exception error)
                {
                    Debug.Log("RPG_ORK_UI_CURRENT_ERROR: " + property.Name + " " + error.GetType().Name);
                }
            }
            foreach (var field in type.GetFields(flags))
            {
                if (!field.Name.Contains("System") && !field.FieldType.Name.Contains("UISystem")) continue;
                var value = field.GetValue(settings);
                Debug.Log("RPG_ORK_UI_CURRENT_FIELD: " + field.Name + " = " +
                    (value == null ? "<null>" : value.GetType().FullName));
                if (value != null) DumpType("Maki.UISystem." + field.Name, value.GetType(), true);
            }
        }

        private static void DumpType(string label, Type type, bool includeAllMembers = false)
        {
            Debug.Log("RPG_ORK_UI_TYPE: " + label + " = " + type.FullName +
                " [" + type.Assembly.GetName().Name + "]");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var property in type.GetProperties(flags).Where(p =>
                includeAllMembers ||
                p.Name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.Name.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.Name.IndexOf("Project", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.Name.IndexOf("Asset", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Debug.Log("RPG_ORK_UI_PROPERTY: " + label + "." + property.Name +
                    " : " + property.PropertyType.FullName +
                    " get=" + (property.GetMethod != null) +
                    " set=" + (property.SetMethod != null));
            }
            foreach (var field in type.GetFields(flags).Where(f =>
                includeAllMembers ||
                f.Name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.Name.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.Name.IndexOf("Project", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.Name.IndexOf("Asset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.Name == "data"))
            {
                Debug.Log("RPG_ORK_UI_FIELD: " + label + "." + field.Name +
                    " : " + field.FieldType.FullName);
            }
            if (includeAllMembers && type.BaseType != null && type.BaseType != typeof(object))
                DumpType(label + ".base", type.BaseType, includeAllMembers: true);
        }
    }
}
