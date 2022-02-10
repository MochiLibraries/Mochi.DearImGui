namespace Mochi.DearImGui.OpenTK;

// These correspond to the GLFW_GAMEPAD_AXIS_* macros in GLFW
internal enum GamepadAxis
{
    LeftX = 0,
    LeftY = 1,
    RightX = 2,
    RightY = 3,
    LeftTrigger = 4,
    RightTrigger = 5,
    Last = RightTrigger,
}
