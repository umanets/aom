# Customizable Flight View System

Welcome to the Customizable Flight View System! This project enables you to personalize and enhance your flight simulation experience through an easy-to-configure input layout and preset system.

**This script is developed based on the original script for GlovePie written by Oleg Bormosov.**
Original source: [YouTube Link](https://youtu.be/uFRTDDxlJS0?si=ArSjSo7CxEFlsbCZ)

## Features

- **User-Friendly Customization:** Adjust input keys and joystick settings to suit your preferences.
- **Advanced Preset System:** Quickly switch between presets tailored for different aircraft.
- **ScrollLock Modes:** Dynamically switch between modes like auto shifts, center, zoom, and custom views using ScrollLock.
- **Voice Feedback:** Get audio confirmation for preset changes, active modes, and flap states.
- **Flap Control:** Seamlessly manage flaps using joystick or keyboard inputs.
- **Diagnostics and Monitoring:** Monitor key system variables in real-time using the built-in diagnostics tools.


## How It Works

1. **Presets:**

   - Each preset is pre-configured for a specific aircraft type (e.g., Lagg, Yakodin, F4).
   - Modify parameters like yaw, pitch, and zoom to match your needs.

2. **Controls:**

   - Use your keyboard and joystick for various flight actions.
   - Activate **Modification Mode** with the ScrollLock key to adjust settings dynamically.
   - Use ScrollLock Modes to:
     - Adjust auto shifts.
     - Recenter your view.
     - Modify zoom levels.
     - Customize views.

3. **Voice Assistance:**

   - Hear notifications about mode changes, preset loads, and flap states.

## Getting Started

### Prerequisites

- A flight simulation setup with a joystick and keyboard.
- **FreePIE software.**
- **TrackIR and OpenTrack software.**

### Installation

1. Clone the repository:
   ```bash
   git clone git@github.com:umanets/aom.git
   cd aom
   ```

2. Run the script using FreePIE:
   - Open FreePIE software.
   - Load the `aom.py` script in FreePIE.
   - **Important:** Run FreePIE as administrator to ensure compatibility with TrackIR.

### Configuration

1. Open the `aom.py` file in a text editor.
2. Locate the section titled `# === USER CONFIGURABLE INPUTS ===`.
3. Modify the keys and joystick buttons to match your setup.

**Tune Mode**  
ScrollLock toogles between Game and Tune mode. 
You can cycle through ScrollLock Modes by pressing `Right Control + Insert`.  
Each mode provides specific adjustments to your flight experience, such as manual X-axis shifts or custom views.
The adjustments is performed with keys: **RightShift +**
- **KeyUp or KeyDown**: y axis
- **KeyLeft or KeyRight**: x axis
- **PgUp or PgDn**: z axis (zoom)
- **Del or End**: yaw axis
- **Ins or Home**: pitch axis

### Profiles for TrackIR and OpenTrack

The repository includes profile files for TrackIR and OpenTrack:

- **OpenTrack:** `AOM_OpenTrackProfile_F7.ini`
- **TrackIR:** `AOM_TrackirProfile_F7.xml`

Make sure to load these profiles in the respective software before starting the script.

### Presets

You can switch presets in-game using:

- **Lagg:** Press `Up Arrow + Num1`
- **Yakodin:** Press `Up Arrow + Num3`
- **F4:** Press `Up Arrow + Num2`
- **Yakodinb:** Press `Up Arrow + Num4`

**Customizing Presets:**  
Each preset is defined in the `presets` dictionary in `aom.py`. You can adjust parameters like yaw, pitch, zoom, and offsets to create your custom configurations.


## Usage

1. Start FreePIE as administrator.
2. Open and Run Trackir with AOM_TrackirProfile_F7 profile. 
3. Load and run the `aom.py` script in FreePIE.
4. Open and Run OpenTrack with AOM_OpenTrackProfile_F7 profile. 
5. Begin your flight simulation.
6. Load Preset
7. Use the predefined controls and Modification Mode for a personalized experience.
8. Enjoy enhanced realism and control.

## Contributing

Feel free to contribute to this project! Submit a pull request or create an issue to share your feedback or suggestions.

## License

This project is licensed under the MIT License.

---



