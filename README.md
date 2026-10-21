# KomSat.Samples

Sample applications demonstrating the use of APIs in the KomSat namespace. The APIs are documented in NHN's
[developer portal](https://utviklerportal.nhn.no/informasjonstjenester/kommunale-samhandlingstjenester).

The examples are written in C# .NET. They are intended for demonstration purposes only, so little focus has
been on robustness or flexibility.

## Ikj.Client

Demonstrates creating, updating and reading of resources using
the [IKJ API](https://utviklerportal.nhn.no/informasjonstjenester/kommunale-samhandlingstjenester/informasjon-fra-kommunal-journal).

The example uses a multitenant HelseID test client owned by NHN. This client has been delegated access to a limited
number of synthetic organizations, see [Configuration.cs](IKJ.Client/Configuration.cs). The client's private key is
included in `jwk.json`. Please note that the private keys of your own HelseID clients should never be shared in any way!

The [HelseID library](https://github.com/NorskHelsenett/HelseID.Library) is used for retrieving access tokens.
