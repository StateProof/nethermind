﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethermind.Config
{
    public class JsonConfigProvider : IConfigProvider
    {
        private IDictionary<Type, IEnumerable<PropertyInfo>> _properties;
        private IDictionary<Type, object> _instances;

        public JsonConfigProvider()
        {
            Initialize();
        }

        public void LoadJsonConfig(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                throw new Exception($"Config file does not exist: {configFilePath}");
            }

            using (var reader = File.OpenText(configFilePath))
            {
                var json = (JArray)JToken.ReadFrom(new JsonTextReader(reader));
                foreach (var moduleEntry in json)
                {
                    LoadModule(moduleEntry);
                }
            }
        }

        public T GetConfig<T>() where T : IConfig
        {
            var moduleType = typeof(T);
            if (_instances.ContainsKey(moduleType))
            {
                return (T)_instances[moduleType];
            }
            throw new Exception($"Config type: {moduleType.Name} is not availible in ConfigModule.");
        }

        private void Initialize()
        {
            var type = typeof(IConfig);
            var modules = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => type.IsAssignableFrom(x) && x.IsClass).ToArray();

            _properties = new Dictionary<Type, IEnumerable<PropertyInfo>>();
            _instances = new Dictionary<Type, object>();
            foreach (var module in modules)
            {
                _instances[module] = Activator.CreateInstance(module);
                _properties[module] = module.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
            }
        }

        private void LoadModule(JToken moduleEntry)
        {
            var configModule = (string) moduleEntry["ConfigModule"];

            var configItems = (JObject) moduleEntry["ConfigItems"];
            var itemsDict = new Dictionary<string, string>();

            foreach (var configItem in configItems)
            {
                if (!itemsDict.ContainsKey(configItem.Key))
                {
                    itemsDict[configItem.Key] = configItem.Value.ToString();
                }
                else
                {
                    throw new Exception($"Duplicated config value: {configItem.Key}, module: {configModule}");
                }
            }

            ApplyConfigValues(configModule, itemsDict);
        }

        private void ApplyConfigValues(string configModule, IDictionary<string, string> items)
        {
            if (!items.Any())
            {
                return;
            }

            var moduleType = _instances.Keys.FirstOrDefault(x => CompareIgnoreCaseTrim(x.Name, $"{configModule}"));
            if (moduleType == null)
            {
                throw new Exception($"Cannot find type with Name: {configModule}");
            }

            var instance = _instances[moduleType];

            foreach (var item in items)
            {
                SetConfigValue(instance, moduleType, item);
            }
        }

        private void SetConfigValue(object configInstance, Type moduleType, KeyValuePair<string, string> item)
        {
            var configProperties = _properties[moduleType];
            var property = configProperties.FirstOrDefault(x => CompareIgnoreCaseTrim(x.Name, item.Key));
            if (property == null)
            {
                throw new Exception($"Incorrent config key, no property on {configInstance.GetType().Name} config: {item.Key}");
            }

            var valueType = property.PropertyType;
            if (valueType.IsArray || (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                //supports Arrays, e.g int[] and generic IEnumerable<T>, IList<T>
                var itemType = valueType.IsGenericType ? valueType.GetGenericArguments()[0]: valueType.GetElementType();

                //In case of collection of objects (more complex config models) we parse entire collection 
                if (itemType.IsClass && typeof(IConfigModel).IsAssignableFrom(itemType))
                {
                    var objCollection = JsonConvert.DeserializeObject(item.Value, valueType);
                    property.SetValue(configInstance, objCollection);
                    return;
                }

                var valueItems = item.Value.Split(',').ToArray();
                var collection = valueType.IsGenericType 
                    ? (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType)) 
                    : (IList)Activator.CreateInstance(valueType, valueItems.Length);

                var i = 0;
                foreach (var valueItem in valueItems)
                {
                    var itemValue = GetValue(itemType, valueItem, item.Key);
                    if (valueType.IsGenericType)
                    {
                        collection.Add(itemValue);
                    }
                    else
                    {
                        collection[i] = itemValue;
                        i++;
                    }
                }

                property.SetValue(configInstance, collection);
                return;
            }
            var value = GetValue(valueType, item.Value, item.Key);
            property.SetValue(configInstance, value);
        }

        private object GetValue(Type valueType, string itemValue, string key)
        {
            if (valueType.IsEnum)
            {
                if (Enum.TryParse(valueType, itemValue, true, out var enumValue))
                {
                    return enumValue;
                }
                throw new Exception($"Cannot parse enum value: {itemValue}, type: {valueType.Name}, key: {key}");
            }

            return Convert.ChangeType(itemValue, valueType);
        }

        private bool CompareIgnoreCaseTrim(string value1, string value2)
        {
            if (string.IsNullOrEmpty(value1) && string.IsNullOrEmpty(value2))
            {
                return true;
            }
            if (string.IsNullOrEmpty(value1) || string.IsNullOrEmpty(value2))
            {
                return false;
            }
            return string.Compare(value1.Trim(), value2.Trim(), StringComparison.CurrentCultureIgnoreCase) == 0;
        }
    }
}