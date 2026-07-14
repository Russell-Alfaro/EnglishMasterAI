using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EnglishMasterAI.Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ─── HttpClient apuntando a la API ───────────────────────────────────────────
// IMPORTANTE: La URL base DEBE ser la de la API (puerto 5050), NO la del
// frontend Blazor. Si el BaseAddress apuntara al frontend recibirías un
// 405 Method Not Allowed porque el servidor estático no acepta POST.
// En producción reemplaza esta URL con la URL real del servidor de API.
const string ApiBaseUrl = "http://localhost:5050/";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(ApiBaseUrl)
});

await builder.Build().RunAsync();
