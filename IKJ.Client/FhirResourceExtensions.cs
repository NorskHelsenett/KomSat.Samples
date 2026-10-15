using Hl7.Fhir.Model;

namespace IKJ.Client;

public static class FhirResourceExtensions
{
    public static string GetName(this Patient patient)
    {
        var name = patient.Name.FirstOrDefault(x => x.Use == HumanName.NameUse.Official);
        return name == null
            ? "<no name>"
            : $"{string.Join(' ', name.Given)} {name.Family}";
    }

    public static string AsString(this Identifier identifier)
    {
        return $"{identifier.System}|{identifier.Value}";
    }

    public static string AsString(this Coding coding)
    {
        return $"{coding.System}|{coding.Code}";
    }
}
