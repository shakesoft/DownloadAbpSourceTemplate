using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DownloadSource
{
    class Program
    {
        private const string ModuleListUrl = "https://abp.io/api/download/modules";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Abp.io 源码下载器");

            ModuleSourceDownloader.InitDirectories();

            var downloadFree = true;
            var downloadPro = true;

            Console.WriteLine("请选择下载类型");
            Console.WriteLine("1. 仅下载免费源码");
            Console.WriteLine("2. 仅下载收费源码");
            Console.WriteLine("3. 下载全部");
            var type = Console.ReadLine();
            if (type == "1")
            {
                downloadPro = false;
                Console.WriteLine("仅下载免费源码");
            }
            else if (type == "2")
            {
                downloadFree = false;
                Console.WriteLine("仅下载收费源码");
            }
            else if (type != "3")
            {
                Console.WriteLine("输入错误，将下载全部源码");
            }

            string token = null;

            if (downloadPro)
            {
                Console.WriteLine("尝试读取收费模块下载令牌");

                // const string tokenFilename = "%USERPROFILE%\\.abp\\cli\\access-token.bin";
                
                string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string abpCliPath = Path.Combine(userProfilePath, ".abp", "cli");
                string tokenFilename = Path.Combine(abpCliPath, "access-token.bin");

                if (File.Exists(tokenFilename))
                {
                    using var sr = new StreamReader(tokenFilename);
                    token = await sr.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("令牌读取失败，请使用 abp login 后再运行本程序");
                    return;
                }
            }

            Console.Write("请输入要下载的版本：");
            var version = Console.ReadLine();

            Console.WriteLine("下载模板源码");

            try
            {
                if (downloadFree)
                {
                    Console.WriteLine("下载免费模板源码...");
                    Console.WriteLine("下载应用程序模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("app", version);
                    Console.WriteLine("下载模块模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("module", version);
                    Console.WriteLine("下载控制台应用模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("console", version);
                    Console.WriteLine("下载 WPF 应用模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("wpf", version);
                }

                if (downloadPro)
                {
                    Console.WriteLine("下载收费模板源码...");
                    Console.WriteLine("下载应用程序模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("app-pro", version, true, token);
                    Console.WriteLine("下载应用程序模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("app-nolayers-pro", version, true, token);
                    Console.WriteLine("下载模块模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("module-pro", version, true, token);
                    Console.WriteLine("下载微服务模板源码...");
                    await ModuleSourceDownloader.DownloadTemplate("microservice-pro", version, true, token);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("下载失败，错误：" + e.Message);
            }

            Console.WriteLine("模板下载完成，按回车继续下载模块源码");
            Console.ReadLine();

            var list = await GetModulesList();
            var proCount = list.Count(m => m.IsPro);
            var freeCount = list.Count(m => !m.IsPro);
            Console.WriteLine("成功获取 " + list.Count + " 个模块信息，" + freeCount + " 个免费模块，" + proCount + " 个收费模块");
            Console.WriteLine("开始下载模块...");

            foreach (var module in list)
            {
                if (module.IsPro && !downloadPro)
                {
                    continue;
                }

                if (!module.IsPro && !downloadFree)
                {
                    continue;
                }

                Console.WriteLine((module.IsPro ? "[收费模块]" : "[免费模块]") + " " + module.Name);

                if (string.IsNullOrWhiteSpace(module.Namespace))
                {
                    Console.WriteLine("模块不正确，跳过。");
                    continue;
                }

                Console.WriteLine("正在下载源码...");
                try
                {
                    await ModuleSourceDownloader.Download(module.Name, version, module.IsPro, token);
                }
                catch (Exception e)
                {
                    Console.WriteLine("下载失败，错误：" + e.Message);
                }
            }

            Console.WriteLine("源码下载完成");
        }

        private static async Task<List<Module>> GetModulesList()
        {
            Console.WriteLine("正在获取可下载模块清单...");

            const string moduleListFilename = "module-list.json";

            var content = string.Empty;

            if (File.Exists(moduleListFilename))
            {
                Console.WriteLine("找到本地下载缓存，正在尝试读取清单。");
                using var sr = new StreamReader(moduleListFilename);
                content = await sr.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Console.WriteLine("本地读取失败，尝试联网下载清单。");

                var client = new HttpClient();
                using var responseMessage = await client.GetAsync(ModuleListUrl);

                if (responseMessage.IsSuccessStatusCode)
                {
                    content = await responseMessage.Content.ReadAsStringAsync();

                    Console.WriteLine("将清单保存到本地，降低联网请求数。");
                    await using var sw = new StreamWriter(new FileStream(moduleListFilename, FileMode.Create));
                    await sw.WriteAsync(content);
                    await sw.FlushAsync();
                    sw.Close();
                }
                else
                {
                    throw new Exception("联网下载清单失败失败，错误代码：" + responseMessage.StatusCode);
                }
            }

            var list = JsonConvert.DeserializeObject<List<Module>>(content);
            return list;
        }
    }
}