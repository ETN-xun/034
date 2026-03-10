


using UnityEngine;

public static class TextureCreator
{
    public static Texture2D CreateTexture(Color color, ShapeType shape)
    {
        return CreateTexture(256, color, shape);
    }

    public static Texture2D CreateTexture(int size, Color color, ShapeType shape)
    {
        var safeSize = Mathf.Max(8, size);
        var texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[safeSize * safeSize];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        switch (shape)
        {
            case ShapeType.Sine:
                DrawCircle(pixels, safeSize, color);
                break;
            case ShapeType.Square:
                DrawSquare(pixels, safeSize, color);
                break;
            case ShapeType.Triangle:
                DrawTriangle(pixels, safeSize, color);
                break;
            default:
                break;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void DrawCircle(Color[] pixels, int size, Color fillColor)
    {
        var radius = (size - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        var borderWidth = 1.5f;  // 细边框
        var softEdge = 0.8f;     // 柔和边缘宽度

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var delta = new Vector2(x, y) - center;
                var distance = delta.magnitude;
                
                // 完全透明的外部
                if (distance > radius + softEdge)
                {
                    continue;
                }

                Color color;
                if (distance < radius - borderWidth)
                {
                    // 填充区域
                    color = fillColor;
                }
                else if (distance < radius)
                {
                    // 边框区域
                    color = new Color(0f, 0f, 0f, 0.7f);
                }
                else
                {
                    // 软边缘（抗锯齿）
                    var edgeFade = (radius + softEdge - distance) / softEdge;
                    edgeFade = Mathf.Clamp01(edgeFade);
                    color = new Color(0f, 0f, 0f, 0.7f * edgeFade);
                }

                pixels[y * size + x] = color;
            }
        }
    }

    private static void DrawSquare(Color[] pixels, int size, Color fillColor)
    {
        var margin = Mathf.Max(3, Mathf.RoundToInt(size * 0.12f));
        var borderWidth = 1.5f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
                if (!inside)
                {
                    continue;
                }

                Color color;
                var distToBorder = Mathf.Min(
                    Mathf.Min(x - margin, size - margin - x),
                    Mathf.Min(y - margin, size - margin - y));

                if (distToBorder > borderWidth)
                {
                    color = fillColor;
                }
                else if (distToBorder > 0)
                {
                    // 柔和边框过渡
                    var fade = distToBorder / borderWidth;
                    color = Color.Lerp(new Color(0f, 0f, 0f, 0.7f), fillColor, fade);
                }
                else
                {
                    color = new Color(0f, 0f, 0f, 0.7f);
                }

                pixels[y * size + x] = color;
            }
        }
    }

    private static void DrawTriangle(Color[] pixels, int size, Color fillColor)
    {
        var p0 = new Vector2(size * 0.5f, size * 0.88f);
        var p1 = new Vector2(size * 0.12f, size * 0.18f);
        var p2 = new Vector2(size * 0.88f, size * 0.18f);
        var borderWidth = 1.2f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                if (!IsPointInTriangle(point, p0, p1, p2))
                {
                    continue;
                }

                var edgeDistance = Mathf.Min(
                    DistancePointToLine(point, p0, p1),
                    Mathf.Min(DistancePointToLine(point, p1, p2), DistancePointToLine(point, p2, p0)));

                Color color;
                if (edgeDistance > borderWidth)
                {
                    color = fillColor;
                }
                else if (edgeDistance > 0.2f)
                {
                    // 柔和边框过渡
                    var fade = (edgeDistance - 0.2f) / borderWidth;
                    fade = Mathf.Clamp01(fade);
                    color = Color.Lerp(new Color(0f, 0f, 0f, 0.7f), fillColor, fade * fade);
                }
                else
                {
                    color = new Color(0f, 0f, 0f, 0.7f);
                }

                pixels[y * size + x] = color;
            }
        }
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var s1 = Sign(p, a, b);
        var s2 = Sign(p, b, c);
        var s3 = Sign(p, c, a);
        var hasNeg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
        var hasPos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static float DistancePointToLine(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = Mathf.Max(0.0001f, ab.magnitude);
        return Mathf.Abs(ab.y * point.x - ab.x * point.y + b.x * a.y - b.y * a.x) / length;
    }
}