using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace receiver_msg_time_req
{
	class Program
	{
		private static ConcurrentBag<(DateTime timestamp, string message)> messageBag = new ConcurrentBag<(DateTime, string)>();
		private static readonly object locker = new object();

		static async Task Main(string[] args)
		{
			Console.WriteLine("Запуск TCP-сервера...");

			int port = 80; // Используем порт 8080
			IPAddress ip = IPAddress.Any; // Слушаем на всех доступных IP адресах

			TcpListener listener = new TcpListener(ip, port);
			listener.Start();
			Console.WriteLine($"Сервер слушает на {ip}:{port}");

			Task logTask = LogMessagesPeriodically(); // Задача для логирования

			while (true)
			{
				TcpClient client = await listener.AcceptTcpClientAsync(); // Ожидание клиента
				_ = Task.Run(() => HandleClient(client)); // Асинхронная обработка клиента
			}
		}

		static async Task HandleClient(TcpClient client)
		{
			NetworkStream stream = client.GetStream();
			byte[] buffer = new byte[1024];

			while (true)
			{
				try
				{
					int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (byteCount == 0) break; // Если клиент закрыл соединение

					string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
					DateTime timestamp = DateTime.Now;
					messageBag.Add((timestamp, message));

					Console.WriteLine($"Принято сообщение: {timestamp:yyyy-MM-dd HH:mm:ss.fff} - {message}");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Ошибка при обработке клиента: {ex.Message}");
					break;
				}
			}

			client.Close(); // Закрываем соединение после обработки
		}

		static async Task LogMessagesPeriodically()
		{
			while (true)
			{
				await Task.Delay(60000);  // Логировать каждые 60 секунд

				if (messageBag.IsEmpty)
				{
					Console.WriteLine("Сообщений для логирования нет.");
					continue;
				}

				lock (locker)
				{
					string logFileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
					using (StreamWriter writer = new StreamWriter(logFileName))
					{
						foreach (var (timestamp, message) in messageBag)
						{
							writer.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} - {message}");
						}
					}

					Console.WriteLine($"Сообщения записаны в файл: {logFileName}");

					messageBag = new ConcurrentBag<(DateTime, string)>(); // Очищаем сообщения
				}
			}
		}
	}
}
