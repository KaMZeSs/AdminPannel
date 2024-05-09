using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AdminPannel
{
    public static class EnvLoader
    {
        public static void LoadFromResource()
        {
            // Получаем имя ресурса для файла .env
            string resourceName = "AdminPannel..env";

            // Используем Assembly для доступа к встраиваемым ресурсам
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream is null) return;
                using (StreamReader reader = new StreamReader(stream))
                {
                    // Читаем содержимое файла
                    string envContent = reader.ReadToEnd();

                    // Разбиваем на строки и устанавливаем переменные среды
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
}
