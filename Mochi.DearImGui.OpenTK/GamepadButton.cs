namespace Mochi.DearImGui.OpenTK;

// These correspond to the GLFW_GAMEPAD_BUTTON_* macros in GLFW
internal enum GamepadButton
{
    A = 0,
    B = 1,
    X = 2,
    Y = 3,
    LeftBumper = 4,
    RightBumper = 5,
    Back = 6,
    Start = 7,
    Guide = 8,
    LeftThumb = 9,
    RightThumb = 10,
    DPadUp = 11,
    DPadRight = 12,
    DPadDown = 13,
    DPadLeft = 14,
    Last = DPadLeft,
    Cross = A,
    Circle = B,
    Square = X,
    Triangle = Y,
}
