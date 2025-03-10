﻿using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Wox.Infrastructure.Image
{
    [Flags]
    public enum ThumbnailOptions
    {
        None = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10
    }

    public class WindowsThumbnailProvider
    {
        public enum SHGFI
        {
            SHGFI_ICON = 0x100,
            SHGFI_LARGEICON = 0x0,
            SHGFI_USEFILEATTRIBUTES = 0x10
        }
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows

        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource GetIcon(string fileName, int width, int height, ThumbnailOptions options)
        {
//            SHFILEINFO _SHFILEINFO = new SHFILEINFO();
//            IntPtr _IconIntPtr = SHGetFileInfo(fileName, 0, ref _SHFILEINFO, (uint)Marshal.SizeOf(_SHFILEINFO), (uint)(SHGFI.SHGFI_ICON | SHGFI.SHGFI_LARGEICON | SHGFI.SHGFI_USEFILEATTRIBUTES));
//            if (_IconIntPtr.Equals(IntPtr.Zero)) return null;

            var _Icon = Icon.ExtractAssociatedIcon(fileName);
            var hBitmap = _Icon.ToBitmap().GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                DeleteObject(hBitmap);
            }
        }

        public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
        {
            var _SHFILEINFO = new SHFILEINFO();
            var _IconIntPtr = SHGetFileInfo(fileName, 0, ref _SHFILEINFO, (uint) Marshal.SizeOf(_SHFILEINFO),
                (uint) (SHGFI.SHGFI_ICON | SHGFI.SHGFI_LARGEICON | SHGFI.SHGFI_USEFILEATTRIBUTES));
            if (_IconIntPtr.Equals(IntPtr.Zero)) return null;
//            Icon _Icon = Icon.FromHandle(_SHFILEINFO.hIcon);
            var hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);

//            var _Icon = Icon.ExtractAssociatedIcon(fileName);
//            IntPtr hBitmap = _Icon.ToBitmap().GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                DeleteObject(hBitmap);
            }
        }

        private static IntPtr GetHBitmap(string fileName, int width, int height, ThumbnailOptions options)
        {
            IShellItem nativeShellItem;
            var shellItem2Guid = new Guid(IShellItem2Guid);
            var retCode = SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out nativeShellItem);

            if (retCode != 0)
                throw Marshal.GetExceptionForHR(retCode);

            var nativeSize = new NativeSize
            {
                Width = width,
                Height = height
            };

            IntPtr hBitmap;
            var hr = ((IShellItemImageFactory) nativeShellItem).GetImage(nativeSize, options, out hBitmap);

            // if extracting image thumbnail and failed, extract shell icon
            if (options == ThumbnailOptions.ThumbnailOnly && hr == HResult.ExtractionFailed)
                hr = ((IShellItemImageFactory) nativeShellItem).GetImage(nativeSize, ThumbnailOptions.None,
                    out hBitmap);

            Marshal.ReleaseComObject(nativeShellItem);

            if (hr == HResult.Ok) return hBitmap;

            throw new COMException($"Error while extracting thumbnail for {fileName}",
                Marshal.GetExceptionForHR((int) hr));
        }

        /// <summary>
        ///     返回系统设置的图标
        /// </summary>
        /// <param name="pszPath">文件路径 如果为""  返回文件夹的</param>
        /// <param name="dwFileAttributes">0</param>
        /// <param name="psfi">结构体</param>
        /// <param name="cbSizeFileInfo">结构体大小</param>
        /// <param name="uFlags">枚举类型</param>
        /// <returns>-1失败</returns>
        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
            uint cbSizeFileInfo, uint uFlags);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        internal interface IShellItem
        {
            void BindToHandler(IntPtr pbc,
                [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                out IntPtr ppv);

            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        internal enum SIGDN : uint
        {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
            PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000
        }

        internal enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int) 0x80070057),
            OutOfMemory = unchecked((int) 0x8007000E),
            NoInterface = unchecked((int) 0x80004002),
            Fail = unchecked((int) 0x80004005),
            ExtractionFailed = unchecked((int) 0x8004B200),
            ElementNotFound = unchecked((int) 0x80070490),
            TypeElementNotFound = unchecked((int) 0x8002802B),
            NoObject = unchecked((int) 0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int) 0x800704C7),
            ResourceInUse = unchecked((int) 0x800700AA),
            AccessDenied = unchecked((int) 0x80030005)
        }

        [ComImportAttribute]
        [GuidAttribute("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            [PreserveSig]
            HResult GetImage(
                [In] [MarshalAs(UnmanagedType.Struct)] NativeSize size,
                [In] ThumbnailOptions flags,
                [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeSize
        {
            private int width;
            private int height;

            public int Width
            {
                set => width = value;
            }

            public int Height
            {
                set => height = value;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
    }
}