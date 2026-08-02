# Cargo Documents Logic

This file explains the flow used by `CargoDocumentsController` for:

- listing cargo documents by cargo id
- viewing a cargo document from FTP

The controller does not have a separate `download` endpoint.  
The `view` endpoint returns the file with `Content-Disposition: inline`, which means the browser can open it directly, and the user can still download it from the browser UI if supported by the file type.

## Request Flow

### 1. List documents for a cargo

Endpoint:

```http
GET /api/cargo-documents/by-cargo/{cargoId}
```

Flow:

1. The controller calls `ICargoDocumentsService.GetByCargoIdAsync(cargoId)`.
2. The service forwards the call to `ICargoDocumentsRepository.GetByCargoIdAsync(cargoId)`.
3. The repository queries the `CargoDocuments` table.
4. It also joins `UserMaster` to populate `CreatedByName`.
5. The API returns the document list as JSON.

### 2. View a single document

Endpoint:

```http
GET /api/cargo-documents/{id}/view
```

Flow:

1. The controller calls `ICargoDocumentsService.GetByIdAsync(id)`.
2. The service forwards the call to the repository.
3. The repository loads the record from `CargoDocuments`.
4. If `FTPLink` exists, the repository adjusts it before returning the model.
5. The controller extracts:
   - `ftpLink`
   - file name from the URL
   - content type from the file extension
6. The controller reads FTP credentials from configuration.
7. The controller tries to download the file from FTP.
8. If the first attempt fails, it tries a host fallback.
9. If download succeeds, the file is returned as bytes with `inline` disposition.
10. If all attempts fail, the API returns `502 Bad Gateway`.

## Controller Logic

File: `Controllers/CargoDocumentsController.cs`

### `GetByCargoId`

This is a simple pass-through endpoint:

```csharp
return Ok(await _service.GetByCargoIdAsync(cargoId));
```

It is used to show all cargo documents linked to a cargo record.

### `ViewDocument`

This is the main FTP file-serving endpoint.

Checks performed:

1. If the document id does not exist, return `404 Not Found`.
2. If `FTPLink` is empty, return `400 Bad Request`.
3. Determine the file name using `Path.GetFileName(ftpLink)`.
4. Determine the content type by extension:
   - `.pdf` -> `application/pdf`
   - `.jpg` / `.jpeg` -> `image/jpeg`
   - `.png` -> `image/png`
   - `.gif` -> `image/gif`
   - anything else -> `application/octet-stream`

FTP download behavior:

- It reads FTP username/password from configuration.
- It tries the original FTP URL first.
- If that fails, it calls `SwapHostFallback(...)`.
- It then calls `DownloadFtpFileAsync(...)` for each attempt.

If bytes are returned:

```csharp
Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
return File(fileBytes, contentType);
```

This tells the browser to display the content inline when possible.

## FTP Fallback Logic

### `SwapHostFallback`

This method swaps between these host values:

- `192.168.0.10`
- `SETUP.FRETLOG.COM`

Purpose:

- support different environments
- handle cases where the stored FTP URL points to a host that is not reachable from the current environment

If neither host is present, the URL is returned unchanged.

### `DownloadFtpFileAsync`

This method does the real FTP download.

Steps:

1. Create a `Uri` from the FTP URL.
2. Try `WebRequestMethods.Ftp.DownloadFile`.
3. Attempt passive mode first, then active mode.
4. If username is present, set network credentials.
5. Read the FTP response stream into memory.
6. Return the file bytes.

If any attempt fails:

- the error is logged
- the method tries the next mode
- if all modes fail, it returns an empty byte array

## Repository Logic

File: `Repositories/CargoDocumentsRepository.cs`

### `GetByCargoIdAsync`

SQL behavior:

```sql
SELECT cd.*,
       COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
FROM [CargoDocuments] cd
LEFT JOIN [UserMaster] um ON um.[UserID] = cd.[CreatedBy]
WHERE cd.[CargoID] = @cargoId
```

This returns all documents for one cargo and enriches the record with creator name.

### `GetByIdAsync`

This loads a single document by `CargoDocumentID`.

After the query:

1. If `FTPLink` is empty, nothing is changed.
2. Backslashes are replaced with forward slashes.
3. Some host values are normalized depending on `Ftp:Server`.
4. Duplicate slashes after `ftp://` are cleaned up.
5. The adjusted link is returned in `cargoDocument.FTPLink`.

## Data Model

File: `Models/CargoDocuments.cs`

Important fields used by this feature:

- `CargoDocumentID`
- `CargoID`
- `DocumentName`
- `DocumentFileType`
- `FTPLink`
- `CreatedByName`

## Configuration Used

The code reads these settings:

- `ConnectionStrings:DefaultConnection`
- `Ftp:Server`
- `Ftp:UserName` or `Ftp:Username`
- `Ftp:Password`

## Important Notes

1. The controller and repository both log FTP username/password state during startup. That helps with debugging, but be careful with sensitive values in logs.
2. The code uses both `Ftp:UserName` and `Ftp:Username` in different places. In .NET configuration this often still works, but it is better to keep the key name consistent everywhere.
3. There are two layers of FTP URL adjustment:
   - repository adjusts the stored `FTPLink`
   - controller applies a host fallback if the first attempt fails
4. If the FTP server is unreachable or the link is invalid, the API returns `502 Bad Gateway`.

## Quick End-to-End Summary

1. Client calls `/api/cargo-documents/by-cargo/{cargoId}` to get the document list.
2. Client selects one document and calls `/api/cargo-documents/{id}/view`.
3. Server loads the document record from SQL.
4. Server resolves the FTP link.
5. Server tries to download the file over FTP.
6. Server returns the file inline to the browser.

