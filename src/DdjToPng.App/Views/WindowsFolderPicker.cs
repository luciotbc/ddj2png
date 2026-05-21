using System.Runtime.InteropServices;

namespace DdjToPng.App.Views;

/// <summary>
/// Shows the modern Windows Explorer folder picker dialog via the
/// IFileOpenDialog COM interface (the same dialog used by File Explorer,
/// VS Code, and other modern Windows applications).
/// </summary>
internal static class WindowsFolderPicker
{
    // ── Flat COM interface (vtable order: IUnknown → IModalWindow → IFileDialog → IFileOpenDialog) ─

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        // IModalWindow
        [PreserveSig] int Show(nint hwndOwner);

        // IFileDialog
        void SetFileTypes(uint cFileTypes, nint rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(nint pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, uint fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid(in Guid guid);
        void ClearClientData();
        void SetFilter(nint pFilter);

        // IFileOpenDialog
        void GetResults(out nint ppenum);
        void GetSelectedItems(out nint ppsai);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint pbc, in Guid bhid, in Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        nint pbc,
        in Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    // ── Constants ─────────────────────────────────────────────────────────────

    // CLSID for the FileOpenDialog COM class
    private static readonly Guid ClsidFileOpenDialog = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");

    // IID for IShellItem (used by SHCreateItemFromParsingName)
    private static readonly Guid IidIShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    private const uint FOS_PICKFOLDERS     = 0x00000020; // select folders, not files
    private const uint FOS_FORCEFILESYSTEM = 0x00000040; // only file-system items
    private const uint SIGDN_FILESYSPATH   = 0x80058000; // return full file-system path

    // HRESULT returned when the user presses Cancel
    private const int E_CANCELLED = unchecked((int)0x800704C7);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the modern Windows folder picker dialog.
    /// </summary>
    /// <param name="ownerHandle">Handle of the owner window (pass <c>Handle</c> from the calling <see cref="Form"/>).</param>
    /// <param name="title">Text shown in the dialog title bar.</param>
    /// <param name="initialDirectory">Optional initial directory; ignored if null or does not exist.</param>
    /// <returns>The selected folder path, or <see langword="null"/> if the user cancelled.</returns>
    public static string? Show(nint ownerHandle, string title, string? initialDirectory = null)
    {
        var comType = Type.GetTypeFromCLSID(ClsidFileOpenDialog)
                      ?? throw new InvalidOperationException("FileOpenDialog COM class is not available on this system.");

        var dialog = (IFileOpenDialog)Activator.CreateInstance(comType)!;
        try
        {
            dialog.SetTitle(title);

            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                if (SHCreateItemFromParsingName(initialDirectory, 0, IidIShellItem, out var folder) == 0)
                    dialog.SetFolder(folder);
            }

            int hr = dialog.Show(ownerHandle);

            if (hr == E_CANCELLED)
                return null;

            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            dialog.GetResult(out var item);
            item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }
}
