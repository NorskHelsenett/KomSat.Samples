using HelseId.Library;
using HelseId.Library.ClientCredentials;
using HelseId.Library.Configuration;
using Hl7.Fhir.Serialization;
using IKJ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

/*
 * To use your own HelseID client:
 * - replace the ClientId below with your own client
 * - replace the contents of jwk.json with your own private key
 *
 */
var helseIdConfiguration = new HelseIdConfiguration
{
    ClientId = "c663e595-23b1-4521-82ff-9cbef3c50c4b",
    Scope = "nhn:ikj/api.read nhn:ikj/api.write",
    IssuerUri = "https://helseid-sts.test.nhn.no"
};

builder
    .Services
    .AddHelseIdClientCredentials(helseIdConfiguration)
    .AddJwkFileForClientAuthentication("jwk.json")
    .AddHelseIdMultiTenant();

builder.Services.AddScoped<IIkjApi, IkjApi>().AddScoped<IkjClient>().AddScoped<AccessTokenKeeper>();

builder
    .Services
    .AddSingleton<FhirJsonDeserializer>()
    .AddSingleton<FhirJsonSerializer>()
    .AddSingleton<InputFhirJsonReader>();

builder.Services.AddHostedService<Application>();

builder.Build().Run();
