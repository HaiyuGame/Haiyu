using System;
using System.Collections.Generic;
using System.Text;

namespace GrayExtension.Exceptions;

/// <summary>
/// 灰度报错
/// </summary>
public class GrayException : Exception
{
    public GrayException(string message)
        : base(message) { }

    public static GrayException ThrowNoOpen => new GrayException("Grayscale is not open");
}
