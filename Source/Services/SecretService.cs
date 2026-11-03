using System.Runtime.InteropServices;

namespace TreasureChest.Source.Services;

public class SecretService
{
    // Private Members:
    private const string LibSecret = "libsecret-1.so.0";
    private const int SchemaAttributeString = 0;
    private const int SchemaFlagsNone = 0;
    private readonly IntPtr _gogSchema = CreateGogSchema();

    private SecretService()
    {
        
    }

    [DllImport(LibSecret, EntryPoint = "secret_schema_new", CharSet = CharSet.Ansi)]
    private static extern IntPtr SecretSchemaNew(
        string name,
        int flags,
        string attributeName, int attributeType,
        IntPtr endMarker
    );
    
    [DllImport(LibSecret, EntryPoint = "secret_password_store_sync", CharSet = CharSet.Ansi)]
    private static extern bool SecretPasswordStoreSync(
        IntPtr schema,
        string? collection,
        string label,
        string password,
        IntPtr cancellable,
        out IntPtr error,
        string attributeKey, string attributeValue,
        IntPtr endMarker
    );
    
    [DllImport(LibSecret, EntryPoint = "secret_password_lookup_sync", CharSet = CharSet.Ansi)]
    private static extern IntPtr SecretPasswordLookupSync(
        IntPtr schema,
        IntPtr cancellable,
        out IntPtr error,
        string attributeKey, string attributeValue,
        IntPtr endMarker
    );

    [DllImport(LibSecret, EntryPoint = "secret_password_clear_sync", CharSet = CharSet.Ansi)]
    private static extern bool SecretPasswordClearSync(
        IntPtr schema,
        IntPtr cancellable,
        out IntPtr error,
        string attributeKey, string attributeValue,
        IntPtr endMarker
    );

    [DllImport(LibSecret, EntryPoint = "secret_password_free")]
    private static extern void SecretPasswordFree(IntPtr password);
    
    public Task<bool> SaveRefreshTokenAsync(string accountName, string token)
    {
        return Task.Run(() => SecretPasswordStoreSync(
            schema: _gogSchema,
            collection: null, // Passing null automatically targets the "Login" default keyring
            label: $"Treasure Chest: GOG ({accountName})",
            password: token,
            cancellable: IntPtr.Zero,
            error: out _,
            attributeKey: "Account",
            attributeValue: accountName,
            endMarker: IntPtr.Zero // Required terminator for varargs (...) in C
        ));
    }
    
    public Task<string?> GetRefreshTokenAsync(string accountName)
    {
        return Task.Run(() =>
        {
            var resultPtr = SecretPasswordLookupSync(
                schema: _gogSchema,
                cancellable: IntPtr.Zero,
                error: out _,
                attributeKey: "Account",  
                attributeValue: accountName,
                endMarker: IntPtr.Zero
            );

            if (resultPtr == IntPtr.Zero) 
                return null;

            var token = Marshal.PtrToStringAnsi(resultPtr);
            SecretPasswordFree(resultPtr); // Clean up the unmanaged memory allocated by libsecret
            
            return token;
        });
    }

    public Task<bool> ClearRefreshTokenAsync(string accountName)
    {
        return Task.Run(() => SecretPasswordClearSync(
            schema: _gogSchema,
            cancellable: IntPtr.Zero,
            error: out _,
            attributeKey: "Account", 
            attributeValue: accountName,
            endMarker: IntPtr.Zero
        ));
    }
    
    private static IntPtr CreateGogSchema()
    {
        return SecretSchemaNew(
            GogSchemaName, 
            SchemaFlagsNone, 
            "Account", SchemaAttributeString, 
            IntPtr.Zero
        );
    }

    // Properties:
    public static SecretService Instance { get; } = new();
    public static string GogSchemaName => SettingsService.ApplicationId + "GogAccount";
}