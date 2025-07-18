# Spatial Anchor Path System for Magic Leap 2 (OpenXR)

This system allows you to create persistent spatial anchor points using the Magic Leap 2 controller and visualize them as a connected path. Built using OpenXR for modern Magic Leap 2 development.

## Features

- **Controller Input**: Pull the trigger to create anchor points at the controller's position
- **Persistent Storage**: Anchor points are saved between sessions
- **Visual Path**: Line renderer connects all anchor points
- **Clear Function**: Use the select button (bumper) to clear all anchor points
- **Visual Feedback**: Anchor points can provide visual feedback when approached
- **OpenXR Compatible**: Uses modern OpenXR input system

## Setup Instructions

### 1. Create Anchor Point Prefab

1. Create a new GameObject in your scene (e.g., a sphere or cube)
2. Add the `AnchorPoint.cs` script to it
3. Set up materials:
   - Assign a default material to the GameObject
   - Create a selected material for visual feedback
   - Assign both materials in the AnchorPoint component
4. Make it a prefab by dragging it to your Project window

### 2. Set Up the FixedPath Component

1. Create an empty GameObject in your scene
2. Add the `FixedPath.cs` script to it
3. Configure the settings in the inspector:
   - **Anchor Point Prefab**: Drag your anchor point prefab here
   - **Path Line Material**: Create a material for the connecting lines
   - **Line Width**: Set the thickness of the path lines (default: 0.01)
   - **Left/Right Controller**: Assign your ActionBasedController components
   - **Use Left Controller**: Toggle to choose which controller to use

### 3. OpenXR Setup

1. Ensure you have the XR Interaction Toolkit package installed
2. Set up your XR Origin and XR Controllers in the scene
3. Make sure your project is configured for Magic Leap 2 with OpenXR
4. The system will automatically find controllers in the scene

### 4. XR Interaction Toolkit Setup

1. Install XR Interaction Toolkit via Package Manager
2. Add XR Origin to your scene
3. Add Action-Based Controllers (Left and Right) as children of the XR Origin
4. Configure the controller input actions in the ActionBasedController components

## Usage

### Creating Anchor Points
- Point your controller where you want to place an anchor
- Pull the trigger (activate action) to create an anchor point
- The anchor will be placed at the controller's current position and orientation

### Managing the Path
- **Add Points**: Pull the trigger at different locations
- **Clear Path**: Press the select button (bumper) to clear all anchor points
- **Persistent**: Anchor points are automatically saved and will persist between app sessions

### Visual Feedback
- Anchor points will pulse when the controller approaches them (if tagged as "Controller")
- The path is visualized with a line connecting all anchor points
- Each anchor point is numbered sequentially

## File Structure

- `FixedPath.cs`: Main script for path management and OpenXR controller input
- `AnchorPoint.cs`: Script for individual anchor point behavior
- `spatial_path.json`: Saved file containing anchor positions and rotations

## Customization

### Visual Customization
- Modify the `AnchorPoint.cs` script to change visual feedback behavior
- Adjust materials and effects in the inspector
- Change line renderer properties in `FixedPath.cs`

### Input Customization
- Modify trigger sensitivity by changing the threshold in `Update()` method
- Add additional button mappings using XR Controller actions
- Change controller hand by toggling `useLeftController` or assigning different controllers

### Storage Customization
- Change save file location by modifying `saveFilePath`
- Add additional data to the `PathData` class
- Implement cloud storage or other persistence methods

## Troubleshooting

### Controller Not Working
- Ensure XR Interaction Toolkit is properly installed
- Check that XR Origin and Action-Based Controllers are set up in your scene
- Verify OpenXR is configured for Magic Leap 2 in Project Settings
- Make sure controller input actions are properly configured

### Anchor Points Not Saving
- Check file permissions for the app's persistent data path
- Ensure the JSON serialization is working properly
- Check console for error messages

### Visual Issues
- Verify materials are assigned correctly
- Check that the LineRenderer component is properly configured
- Ensure anchor point prefab has the necessary components

### OpenXR Issues
- Verify OpenXR is enabled in Project Settings > XR Plug-in Management
- Check that Magic Leap 2 is selected as the target platform
- Ensure XR Interaction Toolkit is up to date

## API Reference

### FixedPath Class
- `CreateAnchorPoint(ActionBasedController)`: Creates a new anchor point at controller position
- `SavePath()`: Saves current anchor points to file
- `LoadSavedPath()`: Loads anchor points from file
- `ClearPath()`: Removes all anchor points and clears the path

### AnchorPoint Class
- `Select()`: Activates visual feedback for the anchor point
- `Deselect()`: Deactivates visual feedback
- `SetAnchorNumber(int)`: Sets the anchor point number

## OpenXR Input Actions

The system uses the following OpenXR input actions:
- **Activate Action**: Trigger pull for creating anchor points
- **Select Action**: Bumper/select button for clearing the path

## Notes

- Anchor points are positioned in world space and will maintain their relative positions
- The system uses Unity's built-in serialization for saving/loading
- Controller input is handled through OpenXR and XR Interaction Toolkit
- All anchor points are automatically saved when new ones are created
- This implementation is compatible with Magic Leap 2 and other OpenXR devices 