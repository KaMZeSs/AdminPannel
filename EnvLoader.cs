﻿using System.IO;
using System.Reflection;
using System.Windows.Markup;

namespace AdminPannel
{
    public static class EnvLoader
    {
        public static void LoadFromResource()
        {
            // Имя ресурса для файла .env
            string resourceName = "AdminPannel..env";

            // Используем Assembly для доступа к встраиваемым ресурсам
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream is null) throw new Exception("No .env in resources");
                using (StreamReader reader = new StreamReader(stream))
                {
                    string envContent = reader.ReadToEnd();

                    string[] lines = envContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        // Разделяем ключ и значение
                        int separatorIndex = line.IndexOf('=');
                        if (separatorIndex > 0)
                        {
                            string key = line.Substring(0, separatorIndex);
                            string value = line.Substring(separatorIndex + 1, line.Length - separatorIndex - 1);
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
            }
            
        }
    }

    public class KVPair
    {
        public string Key { get; set; } = String.Empty;
        public string Value { get; set; } = String.Empty;

        public KeyValuePair<String, String> ToKeyValuePair()
        {
            return KeyValuePair.Create(Key, Value);
        }

        public static KVPair FromKeyValuePair(KeyValuePair<String, String> pair)
        {
            return new KVPair()
            {
                Key = pair.Key,
                Value = pair.Value
            };
        }
    }
}
