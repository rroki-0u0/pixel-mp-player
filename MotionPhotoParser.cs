using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PixelMpPlayer;

public static class MotionPhotoParser
{
    private static readonly byte[] MP4_HEADER = { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 };
    private static readonly byte[] JPEG_END_MARKER = { 0xFF, 0xD9 };
    
    public static MotionPhotoData? ParseMotionPhoto(string filePath)
    {
        try
        {
            var fileBytes = File.ReadAllBytes(filePath);
            
            if (!IsMotionPhoto(fileBytes, filePath))
                return null;
            
            var jpegEndIndex = FindJpegEnd(fileBytes);
            if (jpegEndIndex == -1)
                return null;
            
            return CreateMotionPhotoData(fileBytes, jpegEndIndex, filePath);
        }
        catch
        {
            return null;
        }
    }

    private static MotionPhotoData CreateMotionPhotoData(byte[] fileBytes, int jpegEndIndex, string filePath)
    {
        var jpegData = ExtractJpegData(fileBytes, jpegEndIndex);
        var (mp4Data, hasVideo, videoSource, companionVideoPath) = ExtractVideoData(fileBytes, jpegEndIndex, filePath);
        
        return new MotionPhotoData
        {
            JpegData = jpegData,
            Mp4Data = mp4Data,
            OriginalFilePath = filePath,
            HasVideo = hasVideo,
            VideoSource = videoSource,
            CompanionVideoPath = companionVideoPath
        };
    }
    
    private static bool IsMotionPhoto(byte[] data, string filePath)
    {
        return HasMotionPhotoFileName(filePath) || HasMotionPhotoMetadata(data);
    }

    private static bool HasMotionPhotoFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToUpper();
        return fileName.Contains("MP.") || fileName.Contains(".MP.");
    }

    private static bool HasMotionPhotoMetadata(byte[] data)
    {
        var content = Encoding.UTF8.GetString(data);
        return content.Contains("MotionPhoto") || 
               content.Contains("motionphoto") ||
               content.Contains("GCamera:MotionPhoto");
    }
    
    private static int FindJpegEnd(byte[] data)
    {
        for (int i = data.Length - 2; i >= 0; i--)
        {
            if (data[i] == JPEG_END_MARKER[0] && data[i + 1] == JPEG_END_MARKER[1])
            {
                return i;
            }
        }
        return -1;
    }

    private static byte[] ExtractJpegData(byte[] fileBytes, int jpegEndIndex) =>
        fileBytes[..(jpegEndIndex + 2)];

    private static (byte[] mp4Data, bool hasVideo, string videoSource, string? companionVideoPath) 
        ExtractVideoData(byte[] fileBytes, int jpegEndIndex, string filePath)
    {
        // Try embedded video first
        var (mp4Data, hasVideo, videoSource) = TryExtractEmbeddedVideo(fileBytes, jpegEndIndex);
        if (hasVideo)
            return (mp4Data, hasVideo, videoSource, null);
        
        // Try companion video file
        return TryExtractCompanionVideo(filePath);
    }

    private static (byte[] mp4Data, bool hasVideo, string videoSource) TryExtractEmbeddedVideo(byte[] fileBytes, int jpegEndIndex)
    {
        var videoInfo = ParseXmpVideoInfo(fileBytes);
        var mp4StartIndex = videoInfo.HasVideo ? FindVideoByMetadata(fileBytes, videoInfo) : -1;
        
        if (mp4StartIndex == -1)
        {
            mp4StartIndex = FindMp4Start(fileBytes, jpegEndIndex + 2) ??
                           FindAlternativeMp4Start(fileBytes, jpegEndIndex + 2) ??
                           FindVideoSignatures(fileBytes) ?? -1;
        }
        
        if (mp4StartIndex != -1)
        {
            var mp4Data = ExtractMp4Data(fileBytes, mp4StartIndex);
            var hasVideo = mp4Data.Length > 50;
            var videoSource = hasVideo ? "embedded" : $"embedded (size: {mp4Data.Length} bytes)";
            return (mp4Data, hasVideo, videoSource);
        }
        
        return (Array.Empty<byte>(), false, "");
    }

    private static (byte[] mp4Data, bool hasVideo, string videoSource, string? companionVideoPath) 
        TryExtractCompanionVideo(string filePath)
    {
        var companionVideoPath = FindCompanionVideoFile(filePath);
        if (companionVideoPath != null && File.Exists(companionVideoPath))
        {
            var mp4Data = File.ReadAllBytes(companionVideoPath);
            var hasVideo = mp4Data.Length > 1000;
            return (mp4Data, hasVideo, "companion file", companionVideoPath);
        }
        
        return (Array.Empty<byte>(), false, "", null);
    }

    private static byte[] ExtractMp4Data(byte[] fileBytes, int startIndex) =>
        fileBytes[startIndex..];
    
    private static int? FindMp4Start(byte[] data, int startIndex)
    {
        for (int i = startIndex; i <= data.Length - MP4_HEADER.Length; i++)
        {
            if (IsPatternMatch(data, i, MP4_HEADER))
                return i;
        }
        return null;
    }
    
    private static int? FindAlternativeMp4Start(byte[] data, int startIndex)
    {
        var patterns = new[]
        {
            new byte[] { 0x66, 0x74, 0x79, 0x70 }, // "ftyp"
            new byte[] { 0x6D, 0x6F, 0x6F, 0x76 }, // "moov"
            new byte[] { 0x6D, 0x64, 0x61, 0x74 }  // "mdat"
        };
        
        for (int i = startIndex; i <= data.Length - 4; i++)
        {
            foreach (var pattern in patterns)
            {
                if (IsPatternMatch(data, i, pattern))
                {
                    return i >= 4 ? i - 4 : i;
                }
            }
        }
        return null;
    }
    
    private static int? FindVideoSignatures(byte[] data)
    {
        var signatures = new[] { "ftyp", "moov", "mdat", "mvhd", "trak" };
        
        foreach (var sig in signatures)
        {
            var sigBytes = Encoding.ASCII.GetBytes(sig);
            for (int i = 0; i <= data.Length - sigBytes.Length; i++)
            {
                if (IsPatternMatch(data, i, sigBytes))
                {
                    return i >= 4 ? i - 4 : i;
                }
            }
        }
        return null;
    }

    private static bool IsPatternMatch(byte[] data, int startIndex, byte[] pattern)
    {
        if (startIndex + pattern.Length > data.Length)
            return false;

        return data.AsSpan(startIndex, pattern.Length).SequenceEqual(pattern);
    }
    
    private static string? FindCompanionVideoFile(string imageFilePath)
    {
        var directory = Path.GetDirectoryName(imageFilePath);
        var filenameWithoutExt = Path.GetFileNameWithoutExtension(imageFilePath);
        
        if (directory == null) return null;
        
        var patterns = CreateCompanionFilePatterns(filenameWithoutExt);
        
        foreach (var pattern in patterns)
        {
            var videoPath = Path.Combine(directory, pattern);
            if (File.Exists(videoPath))
                return videoPath;
        }
        
        return SearchRelatedMp4Files(directory, filenameWithoutExt);
    }

    private static string[] CreateCompanionFilePatterns(string baseFilename) => new[]
    {
        $"{baseFilename}.mp4",
        $"{baseFilename}_video.mp4",
        $"{baseFilename}-video.mp4",
        $"{baseFilename}.MP4",
        baseFilename.Replace(".MP.COVER", ".mp4"),
        baseFilename.Replace(".MP.COVER", "")
    };

    private static string? SearchRelatedMp4Files(string directory, string baseFilename)
    {
        try
        {
            var basePattern = baseFilename.Split('_')[0];
            var mp4Files = Directory.GetFiles(directory, "*.mp4", SearchOption.TopDirectoryOnly)
                                   .Concat(Directory.GetFiles(directory, "*.MP4", SearchOption.TopDirectoryOnly));
            
            return mp4Files.FirstOrDefault(mp4File => 
                Path.GetFileNameWithoutExtension(mp4File).StartsWith(basePattern, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
    
    private static VideoMetadata ParseXmpVideoInfo(byte[] data)
    {
        var content = Encoding.UTF8.GetString(data);
        var videoInfo = new VideoMetadata();
        
        var mimeMatch = Regex.Match(content, @"Item:Mime=""video/mp4""");
        if (mimeMatch.Success)
        {
            var lengthPattern = @"Item:Length=""(\d+)""";
            var matches = Regex.Matches(content, lengthPattern);
            
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int length) && length > 100000)
                {
                    videoInfo.HasVideo = true;
                    videoInfo.Length = length;
                    break;
                }
            }
        }
        
        return videoInfo;
    }
    
    private static int FindVideoByMetadata(byte[] data, VideoMetadata videoInfo)
    {
        var ftypBytes = Encoding.ASCII.GetBytes("ftyp");
        
        for (int i = 0; i <= data.Length - ftypBytes.Length; i++)
        {
            if (!IsPatternMatch(data, i, ftypBytes) || i < 4)
                continue;
                
            var startPos = i - 4;
            var remainingSize = data.Length - startPos;
            
            if (IsValidVideoPosition(data, startPos, remainingSize, videoInfo))
                return startPos;
        }
        
        return -1;
    }

    private static bool IsValidVideoPosition(byte[] data, int startPos, int remainingSize, VideoMetadata videoInfo)
    {
        var boxSize = ReadBoxSize(data, startPos);
        
        if (boxSize <= 0 || boxSize >= data.Length)
            return false;
            
        // Check size variance if metadata available
        if (videoInfo.Length > 0)
        {
            var variance = Math.Abs((double)(remainingSize - videoInfo.Length)) / videoInfo.Length;
            if (variance < 0.1) return true;
        }
        
        // Check box size reasonableness
        if (boxSize > 1000000 && Math.Abs(boxSize - remainingSize) < 100)
            return true;
            
        // Check for substantial video chunk with essential boxes
        return remainingSize > 1000000 && ContainsEssentialMp4Boxes(data, startPos);
    }

    private static int ReadBoxSize(byte[] data, int position)
    {
        if (position + 4 > data.Length)
            return -1;
            
        return (data[position] << 24) | (data[position + 1] << 16) | 
               (data[position + 2] << 8) | data[position + 3];
    }
    
    private static bool ValidateMp4Data(byte[] data)
    {
        if (data.Length < 32)
            return false;
        
        try
        {
            var (foundFtyp, foundMdat, foundMoov) = ScanMp4Boxes(data);
            return foundFtyp && (foundMdat || foundMoov);
        }
        catch
        {
            return false;
        }
    }

    private static (bool foundFtyp, bool foundMdat, bool foundMoov) ScanMp4Boxes(byte[] data)
    {
        bool foundFtyp = false, foundMdat = false, foundMoov = false;
        int pos = 0;
        
        while (pos + 8 < data.Length && pos < 10000)
        {
            var boxSize = ReadBoxSize(data, pos);
            if (boxSize <= 8 || boxSize > data.Length - pos)
                break;
                
            if (pos + 8 >= data.Length)
                break;
                
            var boxType = Encoding.ASCII.GetString(data, pos + 4, 4);
            
            switch (boxType)
            {
                case "ftyp": foundFtyp = true; break;
                case "mdat": foundMdat = true; break;
                case "moov": foundMoov = true; break;
            }
            
            pos += boxSize;
        }
        
        return (foundFtyp, foundMdat, foundMoov);
    }
    
    private static bool ContainsEssentialMp4Boxes(byte[] data, int startPos)
    {
        try
        {
            var searchLimit = Math.Min(data.Length, startPos + 50000);
            var (foundFtyp, foundMdat, foundMoov) = ScanMp4BoxesInRange(data, startPos, searchLimit);
            return foundFtyp && foundMdat && foundMoov;
        }
        catch
        {
            return false;
        }
    }

    private static (bool foundFtyp, bool foundMdat, bool foundMoov) ScanMp4BoxesInRange(byte[] data, int startPos, int endPos)
    {
        bool foundFtyp = false, foundMdat = false, foundMoov = false;
        int pos = startPos;
        
        while (pos + 8 < endPos)
        {
            var boxSize = ReadBoxSize(data, pos);
            if (boxSize <= 8 || boxSize > data.Length - pos)
                break;
                
            if (pos + 8 >= data.Length)
                break;
                
            var boxType = Encoding.ASCII.GetString(data, pos + 4, 4);
            
            switch (boxType)
            {
                case "ftyp": foundFtyp = true; break;
                case "mdat": foundMdat = true; break;
                case "moov": foundMoov = true; break;
            }
            
            pos += boxSize;
        }
        
        return (foundFtyp, foundMdat, foundMoov);
    }
}

public class VideoMetadata
{
    public bool HasVideo { get; set; }
    public int Length { get; set; }
}

public class MotionPhotoData
{
    public byte[] JpegData { get; set; } = Array.Empty<byte>();
    public byte[] Mp4Data { get; set; } = Array.Empty<byte>();
    public string OriginalFilePath { get; set; } = string.Empty;
    public bool HasVideo { get; set; }
    public string VideoSource { get; set; } = string.Empty;
    public string? CompanionVideoPath { get; set; }
}