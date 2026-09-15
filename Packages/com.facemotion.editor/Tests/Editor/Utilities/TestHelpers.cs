using System.Collections.Generic;
using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Reflection helpers used to construct deliberately malformed data states that the
    /// public API does not expose. Field names are internal implementation details kept in
    /// sync with the Core layer; a failed assert here signals a structural refactor.
    /// </summary>
    internal static class ReflectionUtil
    {
        public static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Field '{0}' was not found on '{1}'.", fieldName, instance.GetType().FullName);
            field.SetValue(instance, value);
        }

        public static object GetFieldValue(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Field '{0}' was not found on '{1}'.", fieldName, instance.GetType().FullName);
            return field.GetValue(instance);
        }
    }

    internal static class TestHelpers
    {
        public static FaceMotionDiagnostic FindDiagnostic(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            if (diagnostics == null)
            {
                return null;
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null && diagnostics[i].Code == code)
                {
                    return diagnostics[i];
                }
            }

            return null;
        }

        public static FaceMotionDiagnostic FindDiagnostic(ValidationReport report, string code)
        {
            return report == null ? null : FindDiagnostic(report.Diagnostics, code);
        }
    }
}