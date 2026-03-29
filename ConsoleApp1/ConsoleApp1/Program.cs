using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main()
        {
            // Параметры
            const int count = 1000;
            const int min = 0;
            const int max = 5000;

            // Создаём генератор и три анализатора
            var generator = new NumberGenerator(count: count, min: min, max: max);

            var maxAnalyzer = new Analyzer("MaxFinder", data =>
            {
                var maxV = data.Max();
                Console.WriteLine($"[MaxFinder] Max = {maxV}");
            });

            var minAnalyzer = new Analyzer("MinFinder", data =>
            {
                var minV = data.Min();
                Console.WriteLine($"[MinFinder] Min = {minV}");
            });

            var avgAnalyzer = new Analyzer("AvgCalculator", data =>
            {
                var avg = data.Average();
                Console.WriteLine($"[AvgCalculator] Average = {avg:F2}");
            });

            // Подписываем анализаторы на событие генератора
            generator.DataGenerated += maxAnalyzer.OnDataGenerated;
            generator.DataGenerated += minAnalyzer.OnDataGenerated;
            generator.DataGenerated += avgAnalyzer.OnDataGenerated;

            // Запускаем потоки анализаторов — они будут ждать события
            var t1 = new Thread(maxAnalyzer.Run) { Name = "MaxThread" };
            var t2 = new Thread(minAnalyzer.Run) { Name = "MinThread" };
            var t3 = new Thread(avgAnalyzer.Run) { Name = "AvgThread" };

            t1.Start();
            t2.Start();
            t3.Start();

            // Запускаем генерацию в отдельном потоке
            var genThread = new Thread(generator.Generate) { Name = "GeneratorThread" };
            genThread.Start();

            // Ждём завершения всех потоков
            genThread.Join();
            t1.Join();
            t2.Join();
            t3.Join();

            Console.WriteLine("All work completed. Press any key to exit...");
            Console.ReadKey();
        }
    }

    // Класс-генератор: генерирует числа и вызывает событие
    class NumberGenerator
    {
        public event EventHandler<List<int>>? DataGenerated;

        private readonly int _count;
        private readonly int _min;
        private readonly int _max;

        public NumberGenerator(int count, int min, int max)
        {
            _count = count;
            _min = min;
            _max = max;
        }

        public void Generate()
        {
            var rnd = new Random();
            var data = new List<int>(_count);
            Console.WriteLine("Generator: starting generation...");

            for (int i = 0; i < _count; i++)
            {
                data.Add(rnd.Next(_min, _max + 1));
                if ((i + 1) % 250 == 0) // небольшой вывод прогресса
                    Console.WriteLine($"Generator: generated {i + 1} items");
                // Опциональная лёгкая пауза для наглядности
                Thread.Sleep(1);
            }

            Console.WriteLine("Generator: finished generation, raising event...");
            OnDataGenerated(data);
        }

        protected virtual void OnDataGenerated(List<int> data)
        {
            DataGenerated?.Invoke(this, data);
        }
    }

    // Анализатор: подписывается на событие и ожидает сигнала для выполнения анализа в своём потоке
    class Analyzer
    {
        private readonly string _name;
        private readonly Action<List<int>> _analyzeAction;
        private readonly ManualResetEventSlim _signal = new(false);
        private List<int>? _data;

        public Analyzer(string name, Action<List<int>> analyzeAction)
        {
            _name = name;
            _analyzeAction = analyzeAction;
        }

        // Этот метод будет вызван в потоке генератора при возникновении события
        public void OnDataGenerated(object? sender, List<int> data)
        {
            // Сохраняем ссылку на данные и сигналим ожиданию
            _data = data;
            _signal.Set();
        }

        // Метод, который исполняется в отдельном потоке — он ждёт сигнала и затем анализирует
        public void Run()
        {
            Console.WriteLine($"[{_name}] Waiting for data...");
            _signal.Wait(); // ожидаем пока генератор не вызовет OnDataGenerated

            // Копируем данные локально для безопасности
            var localData = _data ?? new List<int>();
            Console.WriteLine($"[{_name}] Starting analysis on {localData.Count} items...");
            try
            {
                _analyzeAction(localData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_name}] Error during analysis: {ex.Message}");
            }
        }
    }
}
