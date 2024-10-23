using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace receiver_msg_time_req
{
	class Program
	{
		private static ConcurrentBag<(DateTime timestamp, string message)> messageBag = new ConcurrentBag<(DateTime, string)>();
		private static readonly object locker = new object();

		static async Task Main(string[] args)
		{
			Console.WriteLine("Запуск TCP-сервера...");

			int port = 8080; // Используем порт 8080
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
					DateTime messageReceivedTime = DateTime.Now; // Время получения сообщения
					messageBag.Add((messageReceivedTime, message));

					Console.WriteLine($"Принято сообщение: {messageReceivedTime:yyyy-MM-dd HH:mm:ss.fff} - {message}");

					// Формируем ответ
					DateTime responseSendTime = DateTime.Now;
					TimeSpan processingTime = responseSendTime - messageReceivedTime;
					string serverIp = ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString();

					var response = new
					{
						MessageReceivedTime = messageReceivedTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
						ResponseSendTime = responseSendTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
						ProcessingTimeMs = processingTime.TotalMilliseconds,
						ServerIp = serverIp
					};

					string responseJson = JsonSerializer.Serialize(response);
					byte[] responseData = Encoding.UTF8.GetBytes(responseJson);

					// Отправляем ответ клиенту
					await stream.WriteAsync(responseData, 0, responseData.Length);

					// Логируем время отправки ответа и время обработки
					Console.WriteLine($"Ответ отправлен: {responseSendTime:yyyy-MM-dd HH:mm:ss.fff}");
					Console.WriteLine($"Время обработки сообщения: {processingTime.TotalMilliseconds} миллисекунд");
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
