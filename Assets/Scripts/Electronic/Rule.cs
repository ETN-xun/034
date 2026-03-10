using System.Collections.Generic;

public enum RuleType
{
    None,
    // 输出器
    SineGenerator,
    SquareGenerator,
    TriangleGenerator,

    // 接收器
    SineReceiver,
    SquareReceiver,
    TriangleReceiver,

    // 将任意输入波形转换为指定输出
    EverythingToSine,
    EverythingToSquare, 
    EverythingToTriangle,
    // 循环转换：正弦波转换为方波，方波转换为三角波，三角波转换为正弦波
    // 反向循环转换：正弦波转换为三角波，方波转换为正弦波，三角波转换为方波
    PositiveCycle,
    NagativeCycle,


    // 添加更多的变换规则类型，并在ConverterCalculator中实现相应的转换逻辑
    // 。。。。。。
}



public static class RuleCalculator
{
    public static ShapeType Generate(RuleType ruleType)
    {
        // 根据不同的生成规则类型进行生成
        switch (ruleType)
        {
            case RuleType.SineGenerator:
                return ShapeType.Sine;
            case RuleType.SquareGenerator:
                return ShapeType.Square;
            case RuleType.TriangleGenerator:
                return ShapeType.Triangle;
            default:
                break;
        }

        return ShapeType.None; // 默认返回None，表示没有匹配的规则
    }


    public static ShapeType Convert(List<ShapeType> input, RuleType ruleType)
    {
        // 根据不同的变换规则类型进行转换
        switch (ruleType)
        {
            case RuleType.EverythingToSine:
                return ShapeType.Sine;
            case RuleType.EverythingToSquare:
                return ShapeType.Square;
            case RuleType.EverythingToTriangle:
                return ShapeType.Triangle;
            case RuleType.PositiveCycle:
                // 正向循环转换逻辑
                if (input[0] == ShapeType.Sine)
                    return ShapeType.Square;
                if (input[0] == ShapeType.Square)
                    return ShapeType.Triangle;
                if (input[0] == ShapeType.Triangle)
                    return ShapeType.Sine;
                break;
            case RuleType.NagativeCycle:
                // 负向循环转换逻辑
                if (input[0] == ShapeType.Sine)
                    return ShapeType.Square;
                if (input[0] == ShapeType.Square)
                    return ShapeType.Triangle;
                if (input[0] == ShapeType.Triangle)
                    return ShapeType.Sine;
                break;

            default:
                break;
        }

        return ShapeType.None; // 默认返回None，表示没有匹配的规则
    }

    public static bool Receive(ShapeType input, RuleType ruleType)
    {
        // 根据不同的接收规则类型进行判断
        switch (ruleType)
        {
            case RuleType.SineReceiver:
                return input == ShapeType.Sine;
            case RuleType.SquareReceiver:
                return input == ShapeType.Square;
            case RuleType.TriangleReceiver:
                return input == ShapeType.Triangle;
            default:
                break;
        }

        return false; // 默认返回false，表示没有匹配的规则
    }
}