using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal static class GwpNativeViewModelExtension
    {
        private static readonly Type? BindingCollectionType = typeof(ViewModel).GetNestedType(
            "DataSourceTypeBindingPropertiesCollection",
            BindingFlags.NonPublic);
        private static readonly FieldInfo? InstanceStorageField = typeof(ViewModel).GetField(
            "_propertiesAndMethods",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? CachedStorageField = typeof(ViewModel).GetField(
            "_cachedViewModelProperties",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly PropertyInfo? PropertiesProperty = BindingCollectionType?.GetProperty(
            "Properties",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo? MethodsProperty = BindingCollectionType?.GetProperty(
            "Methods",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly ConstructorInfo? BindingCollectionConstructor = BindingCollectionType?.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(Dictionary<string, PropertyInfo>),
                typeof(Dictionary<string, MethodInfo>)
            },
            modifiers: null);

        internal static void Attach(ViewModel viewModel, object extension)
        {
            if (!TryGetWritableBindings(viewModel, out Dictionary<string, PropertyInfo>? properties,
                    out Dictionary<string, MethodInfo>? methods)
                || properties == null
                || methods == null)
            {
                return;
            }

            Type extensionType = extension.GetType();
            AddProperty(properties, extensionType, extension, "DeterrenceButtonText");
            AddProperty(properties, extensionType, extension, "DeterrenceButtonHint");
            AddMethod(methods, extensionType, extension, "ExecuteOpenDeterrenceDetails");
            AddMethod(methods, extensionType, extension, "ExecuteLink");
        }

        private static void AddProperty(
            IDictionary<string, PropertyInfo> destination,
            Type extensionType,
            object extension,
            string name)
        {
            PropertyInfo? property = extensionType.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
                destination[name] = new GwpBoundPropertyInfo(property, extension);
        }

        private static void AddMethod(
            IDictionary<string, MethodInfo> destination,
            Type extensionType,
            object extension,
            string name)
        {
            MethodInfo? method = extensionType.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                destination[name] = new GwpBoundMethodInfo(method, extension);
        }

        private static bool TryGetWritableBindings(
            ViewModel viewModel,
            out Dictionary<string, PropertyInfo>? properties,
            out Dictionary<string, MethodInfo>? methods)
        {
            properties = null;
            methods = null;
            if (InstanceStorageField == null
                || CachedStorageField == null
                || PropertiesProperty == null
                || MethodsProperty == null
                || BindingCollectionConstructor == null)
            {
                return false;
            }

            object? storage = InstanceStorageField.GetValue(viewModel);
            IDictionary? cache = CachedStorageField.GetValue(null) as IDictionary;
            if (storage == null || cache == null || !cache.Contains(viewModel.GetType()))
                return false;

            object? cachedStorage = cache[viewModel.GetType()];
            properties = PropertiesProperty.GetValue(storage) as Dictionary<string, PropertyInfo>;
            methods = MethodsProperty.GetValue(storage) as Dictionary<string, MethodInfo>;
            if (properties == null || methods == null)
                return false;

            if (ReferenceEquals(storage, cachedStorage))
            {
                properties = new Dictionary<string, PropertyInfo>(properties);
                methods = new Dictionary<string, MethodInfo>(methods);
                storage = BindingCollectionConstructor.Invoke(new object[] { properties, methods });
                InstanceStorageField.SetValue(viewModel, storage);
            }

            return true;
        }
    }

    internal sealed class GwpBoundPropertyInfo : PropertyInfo
    {
        private readonly PropertyInfo _property;
        private readonly object _instance;

        internal GwpBoundPropertyInfo(PropertyInfo property, object instance)
        {
            _property = property;
            _instance = instance;
        }

        public override PropertyAttributes Attributes => _property.Attributes;
        public override bool CanRead => _property.CanRead;
        public override bool CanWrite => _property.CanWrite;
        public override Type? DeclaringType => _property.DeclaringType;
        public override string Name => _property.Name;
        public override Type PropertyType => _property.PropertyType;
        public override Type? ReflectedType => _property.ReflectedType;

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            MethodInfo[] accessors = _property.GetAccessors(nonPublic);
            MethodInfo[] wrapped = new MethodInfo[accessors.Length];
            for (int index = 0; index < accessors.Length; index++)
                wrapped[index] = new GwpBoundMethodInfo(accessors[index], _instance);
            return wrapped;
        }

        public override object[] GetCustomAttributes(bool inherit) =>
            _property.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
            _property.GetCustomAttributes(attributeType, inherit);

        public override MethodInfo? GetGetMethod(bool nonPublic)
        {
            MethodInfo? method = _property.GetGetMethod(nonPublic);
            return method == null ? null : new GwpBoundMethodInfo(method, _instance);
        }

        public override ParameterInfo[] GetIndexParameters() =>
            _property.GetIndexParameters();

        public override MethodInfo? GetSetMethod(bool nonPublic)
        {
            MethodInfo? method = _property.GetSetMethod(nonPublic);
            return method == null ? null : new GwpBoundMethodInfo(method, _instance);
        }

        public override object? GetValue(object? obj, object?[]? index) =>
            _property.GetValue(_instance, index);

        public override object? GetValue(
            object? obj,
            BindingFlags invokeAttr,
            Binder? binder,
            object?[]? index,
            CultureInfo? culture) =>
            _property.GetValue(_instance, invokeAttr, binder, index, culture);

        public override void SetValue(object? obj, object? value, object?[]? index) =>
            _property.SetValue(_instance, value, index);

        public override void SetValue(
            object? obj,
            object? value,
            BindingFlags invokeAttr,
            Binder? binder,
            object?[]? index,
            CultureInfo? culture) =>
            _property.SetValue(_instance, value, invokeAttr, binder, index, culture);

        public override bool IsDefined(Type attributeType, bool inherit) =>
            _property.IsDefined(attributeType, inherit);
    }

    internal sealed class GwpBoundMethodInfo : MethodInfo
    {
        private readonly MethodInfo _method;
        private readonly object _instance;

        internal GwpBoundMethodInfo(MethodInfo method, object instance)
        {
            _method = method;
            _instance = instance;
        }

        public override MethodAttributes Attributes => _method.Attributes;
        public override RuntimeMethodHandle MethodHandle => _method.MethodHandle;
        public override Type? DeclaringType => _method.DeclaringType;
        public override string Name => _method.Name;
        public override Type? ReflectedType => _method.ReflectedType;
        public override Type ReturnType => _method.ReturnType;
        public override ICustomAttributeProvider ReturnTypeCustomAttributes =>
            _method.ReturnTypeCustomAttributes;

        public override MethodInfo GetBaseDefinition() => _method.GetBaseDefinition();

        public override object[] GetCustomAttributes(bool inherit) =>
            _method.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
            _method.GetCustomAttributes(attributeType, inherit);

        public override MethodImplAttributes GetMethodImplementationFlags() =>
            _method.GetMethodImplementationFlags();

        public override ParameterInfo[] GetParameters() => _method.GetParameters();

        public override object? Invoke(
            object? obj,
            BindingFlags invokeAttr,
            Binder? binder,
            object?[]? parameters,
            CultureInfo? culture) =>
            _method.Invoke(_instance, invokeAttr, binder, parameters, culture);

        public override bool IsDefined(Type attributeType, bool inherit) =>
            _method.IsDefined(attributeType, inherit);
    }
}
