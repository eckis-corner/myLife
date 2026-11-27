using MyCommon.Catalog;

namespace MyLife.Shared.Vault;

/// <summary>Prüfung eines Eintrags vor dem Speichern: Felder nach dem Katalog, dazu die gemeinsamen Angaben von myLife.</summary>
public static class VaultValidation
{
    /// <summary>Prüft alle sichtbaren Felder und die gemeinsamen Angaben. Schlüssel: Feldschlüssel bzw. <c>title</c>/<c>originalLocation</c>/<c>followUpNote</c>.</summary>
    public static Dictionary<string, string> ValidateObject(ObjectTypeDefinition type, VaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            errors["title"] = "required";
        }
        else if (payload.Title.Length > 200)
        {
            errors["title"] = "too-long";
        }

        if (type.OriginalLocationRequired && string.IsNullOrWhiteSpace(payload.OriginalLocation))
        {
            errors["originalLocation"] = "required";
        }

        if (payload.FollowUpNote is { Length: > FieldValues.MaxLongTextLength })
        {
            errors["followUpNote"] = "too-long";
        }

        foreach (var (key, error) in FieldValues.ValidateFields(type, payload))
        {
            errors[key] = error;
        }

        return errors;
    }
}
