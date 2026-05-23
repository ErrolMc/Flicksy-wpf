using System;

namespace Flicksy.Drawing.Media;

public readonly struct VideoFrame
{
    public VideoFrame(byte[] buffer, int bufferLength, int width, int height, int stride, TimeSpan pts)
    {
        Buffer = buffer;
        BufferLength = bufferLength;
        Width = width;
        Height = height;
        Stride = stride;
        Pts = pts;
    }

    public byte[] Buffer { get; }
    public int BufferLength { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public TimeSpan Pts { get; }
}
