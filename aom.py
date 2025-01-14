"""
Customizable Flight View System
=================================

This script enhances your flight simulation experience by providing customizable controls, dynamic mode switching, and advanced diagnostics.

1. **Setup Controls**:
   - To configure controls, modify the `initInputs()` function.
   - This function contains the mappings for keyboard keys and joystick buttons.

2. **Dynamic Preset Switching**:
   - Switch between aircraft presets dynamically using the keyboard or joystick.
   - Presets are stored in the `initPresets()` function, where you can add or modify configurations.

3. **ScrollLock Modes**:
   - Activate **ScrollLock Modes** to dynamically adjust:
       - Auto shifts
       - Centering
       - Zoom in/out
       - Manual or custom views
   - Cycle modes using `Right Control + Insert`.

4. **Flap Control**:
   - Manage flaps using joystick or keyboard inputs.
   - Flap behavior is handled by the `FlapsControl` class.

5. **Voice Feedback**:
   - Audio notifications are provided for active modes, preset changes, and flap state.

6. **Diagnostics**:
   - View system diagnostics in real-time to debug or monitor the script.
   - Use `showDiagnostics()` to track key preset values and state flags.

7. **TrackIR and OpenTrack Integration**:
   - This script integrates seamlessly with TrackIR and OpenTrack for head tracking.

8. **Usage**:
   - Start the script in FreePIE as administrator.
   - Adjust controls and presets as needed.
   - Use the ScrollLock key to access dynamic settings.
"""
import socket
import struct
import math
import time
import ctypes

UDP_IP   = "127.0.0.1"
UDP_PORT = 5555

class StateManager:
	def __init__(self, context):
		self.context = context

	def update_y_axis_state(self, center, shift_1, shift_2):
		"""Update Y-axis state flags in the context."""
		self.context.btn_center_y = center
		self.context.btn_shift_y_1 = shift_1
		self.context.btn_shift_y_2 = shift_2
  
	def update_zoom_state(self, zoom_in, zoom_out):
		"""Update the zoom state flags in the context."""
		self.context.btn_z_zoom_in = zoom_in
		self.context.btn_z_zoom_out = zoom_out

	def toggle_flags(self):
		"""Cycle through setup flags and enable the current one."""
		# Set all flags in the dictionary to False
		for flag in self.context.setupFlags["flags"]:
			self.context.setupFlags[flag] = False

		# Enable the current flag based on the index
		current_flag = self.context.setupFlags["flags"][self.context.setupFlags["current_flag_index"]]
		self.context.setupFlags[current_flag] = True

		# Move to the next flag (cyclically)
		self.context.setupFlags["current_flag_index"] = (
			self.context.setupFlags["current_flag_index"] + 1
		) % len(self.context.setupFlags["flags"])

	def adjust_zoom(self, zoom_key, delta_key, preset_key):
		"""Adjust zoom values in the preset based on input."""
		if keyboard.getKeyDown(zoom_key) and keyboard.getKeyDown(Key.RightShift):
			self.context.preset[preset_key] += 0.01
		if keyboard.getKeyDown(delta_key) and keyboard.getKeyDown(Key.RightShift):
			self.context.preset[preset_key] -= 0.01
	
	def adjust_y(self, y_up_key, y_dn_key, preset_key):
		if keyboard.getKeyDown(y_up_key) and keyboard.getKeyDown(Key.RightShift):
			context.preset[preset_key] += 0.005
		if keyboard.getKeyDown(y_dn_key) and keyboard.getKeyDown(Key.RightShift):
			context.preset[preset_key] -= 0.005
 		
class SystemContext:
	def __init__(self):
		self.input_initialized = False
		self.trackir_initialized = False
		self.presets_initialized = False
		self.state_manager = StateManager(self)

		self.udpSocket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
		

		self.default_preset_loaded = False
		self.flaps_control_initialized = False

		self.setupFlags = {
			"isAuto": False,
			"isZoomIn": False,
			"isZoomOut": False,
			"isCenter": False,
			"isManualShiftX": False,
			"isManualShiftY1": False,
			"isManualShiftY2": False,
			"isCustomView": False,
			"flags": [
				"isAuto",
				"isZoomIn",
				"isZoomOut",
				"isCenter",
				"isManualShiftX",
				"isManualShiftY1",
				"isManualShiftY2",
				"isCustomView",
			],
			"current_flag_index": 0,
			"messages": {
				"isAuto": "setup auto shifts",
				"isZoomIn": "setup zoomout",
				"isZoomOut": "setup zoomin",
    			"isCenter": "setup center",
				"isManualShiftX": "setup manual shift x",
				"isManualShiftY1": "setup manual y one",
				"isManualShiftY2": "setup manual y two",
				"isCustomView": "setup custom view",
			},
		}

		self.flapFlags = {
			"flap_open_start_time": None,
			"flap_close_start_time": None,
			"is_flap_opening": False,
			"is_flap_closing": False,
			"flap_opened": False
		}

		# defaults
		self.manual_yaw = 0
		self.autoCornerX_end = 175
		self.autoCornerEnd   = 126.41
		self.autoCornerStart = 30.0
		self.deltaY0flag = False
		self.deltaX0flag = False
		self.flagYaxis = False
		self.flagDeltaY = True
		self.isSpecialView = False
		self.isGunViewAtCenter = False
		self.isManulalX = False

		self.flap_lastSpeechTime = time.time()
		self.setupMode_lastSpeechTime = time.time()
		
		self.btn_z_zoom_in = False
		self.btn_z_zoom_out = False
		self.btn_shift_y_1 = False
		self.btn_shift_y_2 = False
		self.btn_center_y = True

class FlapsControl:
	def __init__(self, open_keys, close_keys, duration_open=3, duration_close=4):
		"""
		Initialize the flaps control logic.

		Args:
			open_keys (list): Keys for opening the flaps.
			close_keys (list): Keys for closing the flaps.
			duration_open (int): Duration for the flaps to stay open.
			duration_close (int): Duration for the flaps to stay closed.
		"""
		self.open_keys = open_keys
		self.close_keys = close_keys
		self.duration_open = duration_open
		self.duration_close = duration_close
		self.flap_flags = {
			"flap_open_start_time": None,
			"flap_close_start_time": None,
			"is_flap_opening": False,
			"is_flap_closing": False,
			"flap_opened": False,
		}
		self.last_speech_time = time.time()

	def toggle_flap_state(self, is_opening, current_time):
		"""Toggle flap state between opening and closing."""
		self.flap_flags["flap_opened"] = is_opening
		self.flap_flags["is_flap_opening"] = is_opening
		self.flap_flags["is_flap_closing"] = not is_opening
		self.flap_flags["flap_open_start_time"] = current_time if is_opening else None
		self.flap_flags["flap_close_start_time"] = current_time if not is_opening else None

	def handle_flap_action(self, action, current_time):
		"""
		Handle flap action (opening or closing).
		
		Args:
			action (str): Either "is_flap_opening" or "is_flap_closing".
			current_time (float): Current time in seconds.
		"""
		start_time_key = "flap_open_start_time" if action == "is_flap_opening" else "flap_close_start_time"
		duration = self.duration_open if action == "is_flap_opening" else self.duration_close
		keys = self.open_keys if action == "is_flap_opening" else self.close_keys

		if self.flap_flags[start_time_key] is not None and (current_time - self.flap_flags[start_time_key]) < duration:
			for key in keys:
				keyboard.setKeyDown(key)
		else:
			for key in keys:
				keyboard.setKeyUp(key)
			self.flap_flags[action] = False
			self.flap_flags[start_time_key] = None

	def update(self, flap_button_pressed, current_time):
		"""
		Update flap logic based on button press.
		
		Args:
			flap_button_pressed (bool): Whether the flap control button is pressed.
			current_time (float): Current time in seconds.
		"""
		if flap_button_pressed:
			if self.flap_flags["is_flap_opening"] or self.flap_flags["is_flap_closing"]:
				self.toggle_flap_state(not self.flap_flags["is_flap_opening"], current_time)
			else:
				self.toggle_flap_state(not self.flap_flags["flap_opened"], current_time)

		self.handle_flap_action("is_flap_opening", current_time)
		self.handle_flap_action("is_flap_closing", current_time)

		# Voice feedback for flaps
		if (current_time - self.last_speech_time) >= 1.0 and self.flap_flags["flap_opened"]:
			speech.say("Flaps")
			self.last_speech_time = current_time
   
if starting:
	isScrollLock = False
	deltaX, deltaY, deltaZ = 0, 0, 0
	context = SystemContext()

def initInputs(): 
	global MODIFIER_KEYS, JOYSTICK_GUN_VIEW, JOYSTICK_GUN_VIEW, JOYSTICK_X_SHIFT
	global JOYSTICK_CUSTOM_VIEW, GLOBAL_CENTER_KEY, GUNVIEW_CENTERY_KEY
	global GUNVIEW_Y_SHIFT_1_KEY, GUNVIEW_Y_SHIFT_1_KEY, GUNVIEW_Y_SHIFT_2_KEY
	global ZOOM_OUT, ZOOM_IN, FLAPS_BUTTON, INGAME_OPEN_FLAPS, INGAME_CLOSE_FLAPS
	global SCROLL_KEY
	# === USER CONFIGURABLE INPUTS ===
	# Modify these lines to match your input layout
	MODIFIER_KEYS = [Key.LeftAlt, Key.LeftControl, Key.LeftShift]
	# GunView and custom views
	# GunView toggle
	JOYSTICK_GUN_VIEW = {"stick": 0, "btn": 4}
	# Shift view on X-axis
	JOYSTICK_X_SHIFT = {"stick": 0, "btn": 3}
	# Toggle speed indicator view
	JOYSTICK_CUSTOM_VIEW = {"stick": 0, "btn": 2}
	
	# Modification mode (ScrollLock)
	SCROLL_KEY = 0x91   # Scroll Lock
	
	# Centering actions
	GLOBAL_CENTER_KEY = Key.F7


	GUNVIEW_CENTERY_KEY = Key.F1
	GUNVIEW_Y_SHIFT_1_KEY = Key.F2
	GUNVIEW_Y_SHIFT_2_KEY = Key.F3
	ZOOM_OUT = Key.F4
	ZOOM_IN = Key.F5

	# Keyboard commands for flaps, keys from a game
	INGAME_OPEN_FLAPS = [Key.F]
	INGAME_CLOSE_FLAPS = [Key.LeftShift, Key.F]
	# Flaps logic
	FLAPS_BUTTON = 8 # any button, it must NOT be ingame "flaps" button
	
	# --- MODIFICATION MODE INSTRUCTIONS ---
	"""
	MODIFICATION MODE:
	- Activate using ScrollLock
	- Select mode: RightControl + Insert
	- Adjust parameters using:
		- RightShift + Arrow Keys (X/Y axis)
		- RightShift + [DEL, END] (Yaw for custom/auto view)
		- RightShift + [PgUp, PgDn] (Zoom)

	Presets:
	- ArrowUp + Num1: lagg
	- ArrowUp + Num2: f4
	- ArrowUp + Num3: yak 1
	"""
	# === END USER CONFIGURABLE INPUTS ===	
	keyboard.setPressed(GLOBAL_CENTER_KEY)

def initPresets():
	return {
		"lagg": {
			"deltaX1": 0.0007501,
			"deltaY1": 0.0002027,
			"deltaZ1": 0.0005145,
			"deltaX0": 0.24,
			"deltaY0": 0.08,
			"deltaZ2_1": -4.66,
			"deltaZ2_2": 6.66,
			"deltaX2_1": 1.295,
			"deltaY2_1": 0.98,
			"deltaY2_2": 1.45,
			"deltaX2_4": 0.09,
			"deltaY2_4": -6.71,
			"deltaZ2_4": 6.54,
			"syaw": -1.6,
			"spitch": -1.1
		},
		"yakodin": {
			"deltaX1": 0.0007741,
			"deltaY1": 0.0004167,
			"deltaZ1": 0.0005185,
			"deltaX0": 0.049,
			"deltaY0": -0.13,
			"deltaZ2_1": -8.98,
			"deltaZ2_2": 1.93,
			"deltaX2_1": 1.505,
			"deltaY2_1": 1.145,
			"deltaY2_2": 2.489,
			"deltaX2_4": 0.05,
			"deltaY2_4": -6.71,
			"deltaZ2_4": 6.54,
			"syaw": -1,
			"spitch": -1.1
		},
		"f4": {
			"deltaX1": 0.0007920,
			"deltaY1": 0.0002372,
			"deltaZ1": 0.0004746,
			"deltaX0": 0,
			"deltaY0": 0,
			"deltaZ2_1": -3.195,
			"deltaZ2_2": 5.042,
			"deltaX2_1": 3.155,
			"deltaY2_1": 0.67,
			"deltaY2_2": 1.14,
			"deltaX2_4": -1.02,
			"deltaY2_4": -5.39,
			"deltaZ2_4": 6.1,
			"syaw": -1.5,
			"spitch": -1.9
		},
	}

def pollUserInputs():
	global toggleXShift, btn_modifier, btn_globalCenter, toggleGunViewCentered, toggleCustomView
	global toggle_btn_center_y, toggle_btn_shift_y_1, toggle_btn_shift_y_2, btn_flaps
	global toggle_btn_z_zoom_out, toggle_btn_z_zoom_in, toggle_btn_z_zoom_in
	global isScrollLock

	toggleXShift = joystick[JOYSTICK_X_SHIFT["stick"]].getDown(JOYSTICK_X_SHIFT["btn"]) 
	btn_modifier = all(keyboard.getKeyDown(key) for key in MODIFIER_KEYS)
	btn_globalCenter = keyboard.getKeyDown(GLOBAL_CENTER_KEY) and btn_modifier
	toggleGunViewCentered = joystick[JOYSTICK_GUN_VIEW["stick"]].getPressed(JOYSTICK_GUN_VIEW["btn"])

	toggleCustomView = joystick[JOYSTICK_CUSTOM_VIEW["stick"]].getDown(JOYSTICK_CUSTOM_VIEW["btn"])  
	# Y-axis shifts
	toggle_btn_center_y = keyboard.getKeyDown(GUNVIEW_CENTERY_KEY) and btn_modifier
	toggle_btn_shift_y_1 = keyboard.getKeyDown(GUNVIEW_Y_SHIFT_1_KEY) and btn_modifier
	toggle_btn_shift_y_2 = keyboard.getKeyDown(GUNVIEW_Y_SHIFT_2_KEY) and btn_modifier
	# Zoom controls
	toggle_btn_z_zoom_out = keyboard.getKeyDown(ZOOM_OUT) and btn_modifier
	toggle_btn_z_zoom_in = keyboard.getKeyDown(ZOOM_IN) and btn_modifier
	
	#flaps
	btn_flaps = joystick[1].getPressed(FLAPS_BUTTON)

	# edit mode
	GetKeyState = ctypes.windll.user32.GetKeyState
	isScrollLock = (GetKeyState(SCROLL_KEY) & 0x0001) != 0

def handlePresetChange():
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getPressed(Key.NumberPad1):
		speech.say("Loading Lagg preset")
		context.preset = load_preset("lagg")
	
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getPressed(Key.NumberPad3):
		speech.say("Loading yakodin preset")
		context.preset = load_preset("yakodin")
	
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getPressed(Key.NumberPad2):
		speech.say("Loading f4 preset")
		context.preset = load_preset("f4")

def load_preset(preset_name):
	context.preset = context.presets.get(preset_name, {})
	if context.preset:
		#globals().update(preset)
		return context.preset
	else:
		return {}

def compute_deltaX(yaw):
	if not context.isManulalX:
		return 0
	
	if -abs(context.autoCornerX_end) <= yaw <= 0:
		return -abs(context.preset["deltaX2_1"])
	elif 0 < yaw < abs(context.autoCornerX_end):
		return abs(context.preset["deltaX2_1"])
	elif yaw < -abs(context.autoCornerX_end):
		return abs(context.preset["deltaX2_1"] * 3)
	elif abs(context.autoCornerX_end) <= yaw:
		return -abs(context.preset["deltaX2_1"] * 3)

	return 0
	
def compute_autoXYZ(yaw):
	autoX, autoY, autoZ = 0, 0, 0
	
	if yaw != 0:
		if abs(yaw) <= context.autoCornerStart:
			return autoX, autoY, autoZ
		
		if context.autoCornerStart <= abs(yaw) <= context.autoCornerEnd:
			autoX = (yaw * context.preset["deltaX1"]) ** 5
			autoY = (yaw * context.preset["deltaY1"]) ** 4
		else:
			direction = yaw / abs(yaw)  # Normalize yaw direction
			autoX = (direction * context.autoCornerEnd * context.preset["deltaX1"]) ** 5
			autoY = (direction * context.autoCornerEnd * context.preset["deltaY1"]) ** 4

		if context.autoCornerStart <= abs(yaw) <= 165:
			autoZ = (yaw * context.preset["deltaZ1"]) ** 4
		else:
			autoZ = (yaw / abs(yaw) * 165 * context.preset["deltaZ1"]) ** 4

	return autoX, autoY, autoZ

def compute_fake_xyz(yaw, pitch, roll, x, y, z, autoX, autoY, autoZ, deltaX, deltaY, deltaZ):
	fake_yaw = yaw * 0.1
	fake_pitch = pitch
	fake_roll = roll

	fake_temp_x = x * (abs(autoX) == 0) + (autoX * 356000.0)
	fake_temp_x_divider = max(1, abs(fake_temp_x))
	fake_x = (fake_temp_x - fake_temp_x / fake_temp_x_divider) * (abs(fake_temp_x) >= 1 or abs(autoX) > 0) + deltaX * context.isManulalX + context.preset["deltaX0"] * context.deltaX0flag
	fake_y = y * int(context.flagYaxis) + deltaY * int(context.flagDeltaY) + (autoY * 356000.0) + context.preset["deltaY0"] * context.deltaY0flag
	fake_z = z + deltaZ + (autoZ * 356000.0)
	
	return fake_yaw, fake_pitch, fake_roll, fake_x, fake_y, fake_z

def updateTrackir():
	yaw = context.manual_yaw if isScrollLock else trackIR.yaw
	pitch = 0 if isScrollLock else trackIR.pitch
	roll = 0 if isScrollLock else trackIR.roll
	x = 0 if isScrollLock else trackIR.x
	y = 0 if isScrollLock else trackIR.y
	z = 0 if isScrollLock else trackIR.z
	
	context.flagYaxis = abs(yaw) >= 45
	context.flagDeltaY = not (context.autoCornerEnd < abs(yaw) and context.flagYaxis)
	
	# ===================================================================  
	# ================ END SWITCH PRESET LOGIC ==========================
	# ===================================================================
	autoX, autoY, autoZ = compute_autoXYZ(yaw)
	deltaX = compute_deltaX(yaw)

	if context.isSpecialView:
		fake_yaw, fake_pitch, fake_roll = context.preset["syaw"], context.preset["spitch"], 0
		fake_x, fake_y, fake_z = context.preset["deltaX2_4"], context.preset["deltaY2_4"], context.preset["deltaZ2_4"]
	else:
		fake_yaw, fake_pitch, fake_roll, fake_x, fake_y, fake_z = compute_fake_xyz(
			yaw, pitch, roll, x, y, z, autoX, autoY, autoZ, deltaX, deltaY, deltaZ
		)
		
	data = struct.pack('<dddddd', fake_x, fake_y, fake_z, fake_yaw, fake_pitch, fake_roll)
	context.udpSocket.sendto(data, (UDP_IP, UDP_PORT))

def initialize_system(context):
	"""Initialize the system components."""
	if not context.presets_initialized:
		context.presets = initPresets()
		context.presets_initialized = True

	if not context.default_preset_loaded:
		context.preset = load_preset("lagg")
		context.default_preset_loaded = True

	if not context.input_initialized:
		initInputs()
		context.input_initialized = True

	if not context.flaps_control_initialized:
		context.flaps_control = FlapsControl(
			open_keys=INGAME_OPEN_FLAPS,
			close_keys=INGAME_CLOSE_FLAPS,
			duration_open=3,
			duration_close=4
		)
		context.flaps_control_initialized = True

	if not context.trackir_initialized:
		trackIR.update += updateTrackir
		#trackIR.update += lambda: updateTrackir(context)
		context.trackir_initialized = True

def update_deltaY_based_on_state(context):
	"""Update deltaY based on the current Y-axis state."""
	if context.btn_center_y:
		return context.preset.get("deltaY0", 0)
	if context.btn_shift_y_1:
		return context.preset.get("deltaY2_1", 0)
	if context.btn_shift_y_2:
		return context.preset.get("deltaY2_2", 0)
	return 0

def handle_auto_mode(context):
	"""Handle ScrollLock-based auto mode."""
	if keyboard.getKeyDown(Key.Delete) and keyboard.getKeyDown(Key.RightShift):
		context.manual_yaw -= 1
	if keyboard.getKeyDown(Key.End) and keyboard.getKeyDown(Key.RightShift):
		context.manual_yaw += 1
	context.manual_yaw = max(-context.autoCornerEnd, min(-context.autoCornerStart, context.manual_yaw))

	if keyboard.getKeyDown(Key.LeftArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX1"] -= 0.000001
	if keyboard.getKeyDown(Key.RightArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX1"] += 0.000001
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY1"] += 0.000001
	if keyboard.getKeyDown(Key.DownArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY1"] -= 0.000001
	if keyboard.getKeyDown(Key.PageUp) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaZ1"] += 0.000001
	if keyboard.getKeyDown(Key.PageDown) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaZ1"] -= 0.000001

def handle_center_mode(context):
	"""Handle ScrollLock-based center mode."""
	context.manual_yaw = 0
	context.deltaX0flag = True
	context.deltaY0flag = True
	context.state_manager.update_zoom_state(zoom_in=False, zoom_out=True)
	if keyboard.getKeyDown(Key.LeftArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX0"] -= 0.01
	if keyboard.getKeyDown(Key.RightArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX0"] += 0.01
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY0"] -= 0.01
	if keyboard.getKeyDown(Key.DownArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY0"] += 0.01

def handle_manual_shift_x_mode(context):
	"""Handle ScrollLock-based manual shift X mode."""
	context.manual_yaw = 0
	context.deltaX0flag = False
	context.deltaY0flag = False

	if keyboard.getKeyDown(Key.LeftArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX2_1"] += 0.005
	if keyboard.getKeyDown(Key.RightArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX2_1"] -= 0.005
	
def handle_zoom_in_mode(context):
	context.manual_yaw = 0
	context.state_manager.update_zoom_state(zoom_in=True, zoom_out=False)
	context.state_manager.adjust_zoom(Key.PageUp, Key.PageDown, "deltaZ2_1")

def handle_zoom_out_mode(context):
	context.manual_yaw = 0
	context.state_manager.update_zoom_state(zoom_in=False, zoom_out=True)
	context.state_manager.adjust_zoom(Key.PageUp, Key.PageDown, "deltaZ2_2")
	
def handle_manual_shift_y1_mode(context):
	"""Handle ScrollLock-based manual shift Y1 mode."""
	context.manual_yaw = 0
	context.deltaX0flag = True
	context.deltaY0flag = True
	context.state_manager.update_zoom_state(zoom_in=False, zoom_out=False)
	context.state_manager.update_y_axis_state(center=False, shift_1=True, shift_2=False)
	context.state_manager.adjust_y(Key.UpArrow, Key.DownArrow, "deltaY2_1") 

def handle_manual_shift_y2_mode(context):
	"""Handle ScrollLock-based manual shift Y2 mode."""
	context.manual_yaw = 0
	context.deltaX0flag = True
	context.deltaY0flag = True
	context.state_manager.update_zoom_state(zoom_in=False, zoom_out=False)
	context.state_manager.update_y_axis_state(center=False, shift_1=False, shift_2=True)
	context.state_manager.adjust_y(Key.UpArrow, Key.DownArrow, "deltaY2_2") 

def handle_custom_view_mode(context):
	"""Handle ScrollLock-based custom view mode."""
	context.deltaX0flag = False
	context.deltaY0flag = False
	context.btn_shift_y_1 = False
	context.btn_shift_y_2 = False

	if keyboard.getKeyDown(Key.Delete) and keyboard.getKeyDown(Key.RightShift):
		context.preset["syaw"] -= 0.1
	if keyboard.getKeyDown(Key.End) and keyboard.getKeyDown(Key.RightShift):
		context.preset["syaw"] += 0.1
	if keyboard.getKeyDown(Key.Insert) and keyboard.getKeyDown(Key.RightShift):
		context.preset["spitch"] -= 0.1
	if keyboard.getKeyDown(Key.Home) and keyboard.getKeyDown(Key.RightShift):
		context.preset["spitch"] += 0.1
	if keyboard.getKeyDown(Key.LeftArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX2_4"] -= 0.01
	if keyboard.getKeyDown(Key.RightArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaX2_4"] += 0.01
	if keyboard.getKeyDown(Key.UpArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY2_4"] += 0.01
	if keyboard.getKeyDown(Key.DownArrow) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaY2_4"] -= 0.01
	if keyboard.getKeyDown(Key.PageUp) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaZ2_4"] += 0.01
	if keyboard.getKeyDown(Key.PageDown) and keyboard.getKeyDown(Key.RightShift):
		context.preset["deltaZ2_4"] -= 0.01

def handle_scroll_lock_modes(context):
	"""Dispatch handling of ScrollLock-based modes using mode mapping."""
	MODE_HANDLERS = {
		"isAuto": handle_auto_mode,
		"isCenter": handle_center_mode,
		"isZoomIn": handle_zoom_in_mode,
  		"isZoomOut": handle_zoom_out_mode,
		"isManualShiftX": handle_manual_shift_x_mode,
		"isManualShiftY1": handle_manual_shift_y1_mode,
		"isManualShiftY2": handle_manual_shift_y2_mode,
		"isCustomView": handle_custom_view_mode,
	}
	for flag, handler in MODE_HANDLERS.items():
		if context.setupFlags.get(flag, False):
			handler(context)
			break  # Only handle one active mode at a time

def handle_speech_notifications(context, current_time, is_scroll_lock):
	"""
	Handle speech notifications for setup modes and flap status.

	Args:
		context (SystemContext): The application context.
		current_time (float): The current time in seconds.
		is_scroll_lock (bool): Whether ScrollLock is active.
	"""
	# Notify about setup modes
	if (current_time - context.setupMode_lastSpeechTime) >= 2.0 and is_scroll_lock:
		mode_selected = False  # Initialize flag to track if a mode is selected

		# Check if any mode is active
		for flag in context.setupFlags["flags"]:
			if context.setupFlags[flag]:  # If a mode is active
				speech.say(context.setupFlags["messages"][flag])
				context.setupMode_lastSpeechTime = current_time
				mode_selected = True
				break

		# If no mode is active, prompt the user to choose a mode
		if not mode_selected:
			speech.say("Choose setup mode")
			context.setupMode_lastSpeechTime = current_time

	# Indicate flap opened by voice
	if (current_time - context.flap_lastSpeechTime) >= 1.0 and context.flapFlags["flap_opened"]:
		speech.say("flaps")
		context.flap_lastSpeechTime = current_time

def handle_toggles_and_flags(context):
	"""
	Handle toggles and flags updates based on user inputs.

	Args:
		context (SystemContext): The application context.
	"""
	# Handle Y-axis state updates
	if toggle_btn_center_y:
		context.state_manager.update_y_axis_state(center=True, shift_1=False, shift_2=False)

	if toggle_btn_shift_y_1:
		context.state_manager.update_y_axis_state(center=False, shift_1=True, shift_2=False)

	if toggle_btn_shift_y_2:
		context.state_manager.update_y_axis_state(center=False, shift_1=False, shift_2=True)

	# Handle Zoom state updates
	if toggle_btn_z_zoom_out:
		context.state_manager.update_zoom_state(zoom_in=False, zoom_out=True)

	if toggle_btn_z_zoom_in:
		context.state_manager.update_zoom_state(zoom_in=True, zoom_out=False)

	# Handle global centering
	if btn_globalCenter:
		context.manual_yaw = 0
		keyboard.setPressed(GLOBAL_CENTER_KEY)
		context.deltaX0flag = False
		context.deltaY0flag = False

	# Handle GunView centering toggle
	if toggleGunViewCentered:
		context.isGunViewAtCenter = not context.isGunViewAtCenter
		context.deltaX0flag = context.isGunViewAtCenter
		context.deltaY0flag = context.isGunViewAtCenter

	# Handle flag toggling
	if keyboard.getPressed(Key.Insert) and keyboard.getKeyDown(Key.RightControl) and isScrollLock:
		context.state_manager.toggle_flags()

def showDiagnostics():
	diagnostics.watch(context.preset["deltaX1"])
	diagnostics.watch(context.preset["deltaY1"])
	diagnostics.watch(context.preset["deltaZ1"])	

		
	diagnostics.watch(context.preset["deltaX0"])
	diagnostics.watch(context.preset["deltaY0"])

	diagnostics.watch(context.preset["deltaZ2_1"])
	diagnostics.watch(context.preset["deltaZ2_2"])

	diagnostics.watch(context.preset["deltaX2_1"])

	diagnostics.watch(context.preset["deltaY2_1"])

	diagnostics.watch(context.preset["deltaY2_2"])

	diagnostics.watch(context.preset["deltaX2_4"])
	diagnostics.watch(context.preset["deltaY2_4"])
	diagnostics.watch(context.preset["deltaZ2_4"])		
	diagnostics.watch(context.preset["syaw"])
	diagnostics.watch(context.preset["spitch"])
	
# Initialize system components (presets, inputs, TrackIR, flaps control)
initialize_system(context)

# Poll user inputs (e.g., joystick, keyboard, modifiers)
pollUserInputs()

# Handle preset changes based on user input (e.g., switching presets)
handlePresetChange()

# Update toggles and flags (e.g., Y-axis states, zoom states, global center)
handle_toggles_and_flags(context)

# Compute delta values based on current state and input
deltaY = update_deltaY_based_on_state(context)
deltaZ = (
	context.preset["deltaZ2_1"] * context.btn_z_zoom_in +
	context.preset["deltaZ2_2"] * context.btn_z_zoom_out
)

# Update special view and manual X-axis control flags
context.isSpecialView = toggleCustomView or (isScrollLock and context.setupFlags["isCustomView"])
context.isManulalX = toggleXShift or (isScrollLock and context.setupFlags["isManualShiftX"])

# Handle ScrollLock-specific mode updates (e.g., auto, center, custom view)
if isScrollLock:
	handle_scroll_lock_modes(context)

# Get the current time for time-dependent updates
current_time = time.time()

# Update flaps control logic based on button presses and timing
context.flaps_control.update(flap_button_pressed=btn_flaps, current_time=current_time)

# Provide speech notifications for setup modes and flap status
handle_speech_notifications(context, current_time, isScrollLock)

# Display diagnostics for debugging and monitoring
showDiagnostics()
