using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ConsoleApp13
{
    internal class Model
    {
        public float[] ModelPrediction()
        {
            // Загрузка файла
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "btc_klines_5m_with_time.csv");

            // === Предобработка CSV: удаляем заголовок и все колонки кроме open, high, low, close ===
            var allLines = File.ReadAllLines(filePath).Skip(1); // Пропускаем заголовок

            // Оставляем только нужные колонки (1, 2, 3, 4) и перезаписываем файл
            var cleanedLines = allLines
                .Select(line =>
                {
                    var parts = line.Split(',');
                    // Проверка на валидность (чтобы не было пустых строк)
                    if (parts.Length >= 5)
                        return string.Join(",", parts[1], parts[2], parts[3], parts[4]); // open, high, low, close
                    else
                        return null;
                })
                .Where(line => line != null)
                .ToList();

            File.WriteAllLines(filePath, cleanedLines); // Перезаписываем файл уже без времени и заголовков
            Console.WriteLine("Файл очищен: удалены заголовок и лишние колонки!");

            // ===================== ДАЛЕЕ РАБОТАЕМ С ФАЙЛОМ ==========================

            // Загрузка модели ONNX
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model_predict_close_priceV3.onnx");
            var session = new InferenceSession(modelPath);
            Console.WriteLine("Модель успешно загружена!");

            // Загрузка уже очищенного файла
            var df = File.ReadLines(filePath).Select(line => line.Split(',')).ToArray();

            Console.WriteLine($"Всего собрано свечей: {df.Length}");

            // Берем первые 25 строк
            var rowsForFeatures = df.Take(25).ToArray(); // !!! Take(25)

            // Проверяем, что строк хватает
            if (rowsForFeatures.Length < 25)
            {
                Console.WriteLine($"Ошибка: доступно только {rowsForFeatures.Length} строк для признаков, требуется 25.");
                return [-1];
            }

            // Подготовка признаков
            var features = rowsForFeatures
                .Select(row => new float[]
                {
                    float.Parse(row[0], CultureInfo.InvariantCulture), // open
                    float.Parse(row[1], CultureInfo.InvariantCulture), // high
                    float.Parse(row[2], CultureInfo.InvariantCulture), // low
                    float.Parse(row[3], CultureInfo.InvariantCulture)  // close
                }).ToArray();

            Console.WriteLine($"Форма признаков перед reshape: {features.GetLength(0)} x {features[0].Length}");

            // !!! ВАЖНО: Модель ждёт (1, 25, 4), а не (1, 24, 4)
            var inputTensor = new DenseTensor<float>(features.SelectMany(x => x).ToArray(), new[] { 1, 25, 4 });

            // Вход для модели
            var inputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("input", inputTensor) };

            // Запуск предсказания
            var results = session.Run(inputs);

            // Извлечение результата
            var output = results.FirstOrDefault()?.AsTensor<float>();
            var predictions = output?.ToArray();

            if (predictions == null)
            {
                Console.WriteLine("Ошибка предсказания.");
                return[-1];
            }

            // Базовая цена (последняя "close" цена в 1-й строке CSV (после заголовка))
            var baseline = float.Parse(df[0][3], CultureInfo.InvariantCulture);
            float prevPrice = baseline;

            for (int i = 0; i < predictions.Length; i++)
            {
                float pred = predictions[i];

                predictions[i] = prevPrice * (1 + pred);  // восстановленная цена

            }

            return predictions;
        }
    }
}
