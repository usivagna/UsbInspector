namespace UsbInspector.Core.Services;

/// <summary>Maps USB base class codes to human-readable names (usb.org base class list).</summary>
public static class UsbClassNames
{
    public static string ForClass(byte classCode) => classCode switch
    {
        0x00 => "Defined at Interface level",
        0x01 => "Audio",
        0x02 => "Communications and CDC Control",
        0x03 => "Human Interface Device (HID)",
        0x05 => "Physical",
        0x06 => "Image",
        0x07 => "Printer",
        0x08 => "Mass Storage",
        0x09 => "Hub",
        0x0A => "CDC Data",
        0x0B => "Smart Card",
        0x0D => "Content Security",
        0x0E => "Video",
        0x0F => "Personal Healthcare",
        0x10 => "Audio/Video Devices",
        0x11 => "Billboard",
        0x12 => "USB Type-C Bridge",
        0xDC => "Diagnostic Device",
        0xE0 => "Wireless Controller",
        0xEF => "Miscellaneous",
        0xFE => "Application Specific",
        0xFF => "Vendor Specific",
        _ => $"Class 0x{classCode:X2}",
    };
}
