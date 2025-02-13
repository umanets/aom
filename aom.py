from abc import ABCMeta, abstractmethod
import socket
import struct
import math
import time
import ctypes

import clr
clr.AddReference("System.Windows.Forms")
from System.Windows.Forms import Clipboard
from System.Threading import Thread, ThreadStart, ApartmentState

def set_clipboard_text(text):
	def run():
		try:
			Clipboard.SetText(text)
			diagnostics.debug("Clipboard text set successfully.")
		except Exception as e:
			diagnostics.debug("Error setting clipboard text: " + str(e))
	thread_start = ThreadStart(run)
	sta_thread = Thread(thread_start)
	sta_thread.SetApartmentState(ApartmentState.STA)
	sta_thread.Start()
	sta_thread.Join() 

# === USER CONFIGURABLE INPUTS ===
THORTLE = 0
STICK = 1
MODIFIER_KEYS = [Key.LeftAlt, Key.LeftControl, Key.LeftShift]
btn_modifier = all(keyboard.getKeyDown(key) for key in MODIFIER_KEYS)
GlobalCenterKey = Key.F7
isSideView = joystick[THORTLE].getDown(3)
centerAll = joystick[THORTLE].getDown(45)
centerGunView = joystick[THORTLE].getPressed(4)
centerVGunView = keyboard.getPressed(Key.F1)
head_y_high = joystick[THORTLE].getPressed(60)
head_y_highest = joystick[THORTLE].getPressed(59)
head_y_dynamic = keyboard.getPressed(Key.F6)
zoomOut = keyboard.getPressed(Key.F4)
zoomIn = keyboard.getPressed(Key.F5)
zoomCenter = joystick[THORTLE].getPressed(19)
openFlaps = joystick[STICK].getPressed(1)
closeFlaps = joystick[STICK].getPressed(0)
sixNotifier = keyboard.getPressed(Key.F12)
isCustomView = joystick[THORTLE].getDown(2)
stick_y = joystick[STICK].y
ingame_flaps_release = [Key.F]
ingame_flaps_retract = [Key.F, Key.LeftShift]

RESET_PRESET = "reset"
LAGG_PRESET = "lagg"
YAKONE_PRESET = "yakodin"
F_FOUR_PRESET = "f4"
YAKONE_B_PRESET = "yakodin and b"
LA_FIVE_PRESET = "la five"

presets_map = {
	Key.NumberPad0: RESET_PRESET,
	Key.NumberPad1: LAGG_PRESET,
	Key.NumberPad3: YAKONE_PRESET,
	Key.NumberPad2: F_FOUR_PRESET,
	Key.NumberPad4: YAKONE_B_PRESET,
	Key.NumberPad4: LA_FIVE_PRESET,
}

# opentrack input params for "UDP over network"
UDP_IP   = "127.0.0.1"
UDP_PORT = 5555

def isScrollLock():
	GetKeyState = ctypes.windll.user32.GetKeyState
	return (GetKeyState(0x91) & 0x0001) != 0

class TuneMode:
	def __init__(self, name = "undefined"):
		self._name = name
	
	def modify(self, state, subject, delta):
		mode = TuneModes.Mode(self._name)
		var = mode["vars"].get(subject)
		if not var:
			return
		mapper = lambda value, state, delta: (
			value if mode.get("mappers") is None or mode["mappers"].get(subject) is None
			else mode["mappers"][subject](value, state, delta)
		)
		state.preset[var] = mapper(state.preset.get(var, 0) + delta, state, delta)
	
	@property
	def name(self): return self._name
	
	@name.setter
	def name(self, new_name): 
		self._name = new_name

class Presets:
	all = {
		RESET_PRESET: {
			"deltaX1": 0,
			"deltaY1": 0,
			"deltaZ1": 0,
			"deltaX0": 0,
			"deltaY0": 0,
			"deltaZ2_1": 0,
			"deltaZ2_2": 0,
			"deltaX2_1": 0,
			"deltaY2_1": 0,
			"deltaY2_2": 0,
			"deltaX2_4": 0,
			"deltaY2_4": 0,
			"deltaZ2_4": 0,
			"deltaYLow": 0,
   			"deltaYHigh": 0,
			"syaw": 0,
			"spitch": 0
		},
		LAGG_PRESET: {
			"deltaX0": 0.2500,
			"deltaX1": 1.2804,
			"deltaX2_1": 1.6000,
			"deltaX2_4": 0.0,
			"deltaY0": 0.1000,
			"deltaY1": -0.1304,
			"deltaY2_1": 1.1000,
			"deltaY2_2": 1.6000,
			"deltaY2_4": -4.4500,
			"deltaYHigh": 1.4000,
			"deltaYLow": -0.6500,
			"deltaZ1": 6.5200,
			"deltaZ2_1": -4.0,
			"deltaZ2_2": 7.4900,
			"deltaZ2_4": 8.4500,
			"manual_yaw": -140.0,
			"spitch": -1.0,
			"syaw": -2.5000,
		},
		YAKONE_PRESET: {
			"deltaX0": 0.0400,
			"deltaX1": 1.4504,
			"deltaX2_1": 2.5500,
			"deltaX2_4": -0.4500,
			"deltaY0": -0.0100,
			"deltaY1": 2.8100,
			"deltaY2_1": 1.1000,
			"deltaY2_2": 3.4500,
			"deltaY2_4": -5.0400,
			"deltaYHigh": 3.7000,
			"deltaYLow": -1.0500,
			"deltaZ1": 6.6000,
			"deltaZ2_1": -4.2000,
			"deltaZ2_2": 8.6900,
			"deltaZ2_4": 6.4400,
			"manual_yaw": -140.0000,
			"spitch": -1.5000,
			"syaw": -1.0000,
		},
		F_FOUR_PRESET: {
			"deltaX1": 1.58,
			"deltaY1": 0.94,
			"deltaZ1": 5.02,
			"deltaX0": 0,
			"deltaY0": 0.22,
			"deltaZ2_1": -5.85,
			"deltaZ2_2": 8.75,
			"deltaX2_1": 3.5,
			"deltaY2_1": 0.75,
			"deltaY2_2": 3.05,
			"deltaX2_4": 0,
			"deltaY2_4": -5.35,
			"deltaZ2_4": 3.99,
			"deltaYLow": -0.55,
   			"deltaYHigh": 2.55,
			"syaw": -1.5,
			"spitch": -1
		},
  		YAKONE_B_PRESET: {
			"deltaX1": 1.18,
			"deltaY1": 2.19,
			"deltaZ1": 1.04,
			"deltaX0": 0.0,
			"deltaY0": 0.1,
			"deltaZ2_1": -4.99,
			"deltaZ2_2": 5.74,
			"deltaX2_1": 3.55,
			"deltaY2_1": 0.55,
			"deltaY2_2": 3.6,
			"deltaX2_4": -0.3,
			"deltaY2_4": -5.95,
			"deltaZ2_4": 5.29,
			"deltaYLow": -0.9,
   			"deltaYHigh": 2.3,
			"syaw": -2.5,
			"spitch": -2
		},
		LA_FIVE_PRESET: {
			"deltaX0": 0.0,
			"deltaX1": 1.1904,
			"deltaX2_1": 3.2500,
			"deltaX2_4": -0.2500,
			"deltaY0": 0.0,
			"deltaY1": -2.7200,
			"deltaY2_1": 1.2500,
			"deltaY2_2": 1.4500,
			"deltaY2_4": -3.9500,
			"deltaYHigh": 1.2500,
			"deltaYLow": -0.7500,
			"deltaZ1": 6.7804,
			"deltaZ2_1": -4.3000,
			"deltaZ2_2": 6.3000,
			"deltaZ2_4": 0.3500,
			"manual_yaw": -134.5000,
			"spitch": -1.0000,
			"syaw": -2.0000,
		}
	}

class TuneModes: 
	MODES = [
		{
	  		"name": "isAuto", 			
			"vars": {
				"yaw": "manual_yaw", 
				"x": "deltaX1", 
				"y": "deltaY1", 
				"z": "deltaZ1"
			},
			"mappers": {
				"yaw": lambda x, state, delta: max(-state.autoCornerEnd, min(-state.autoCornerStart, x)),
				"x": lambda x, state, delta: (
					max(0.0004, x) if delta > 0 and x > -0.0004 else min(-0.0004, x) if delta < 0 and x < 0.0004 else x
				),
				"y": lambda x, state, delta: (
					max(0.0004, x) if delta > 0 and x > -0.0004 else min(-0.0004, x) if delta < 0 and x < 0.0004 else x
				),
				"z": lambda x, state, delta: (
					max(0.0004, x) if delta > 0 and x > -0.0004 else min(-0.0004, x) if delta < 0 and x < 0.0004 else x
				),
			}
   		},
		{"name": "isZoomIn", 		"vars": {"z": "deltaZ2_1"}},
		{"name": "isZoomOut", 		"vars": {"z": "deltaZ2_2"}},
		{"name": "isCenter", 		"vars": {"x": "deltaX0", "y": "deltaY0"}},
		{"name": "isManualShiftX", 	"vars": {"x": "deltaX2_1"}},
		{"name": "isManualShiftY1", "vars": {"y": "deltaY2_1"}},
		{"name": "isManualShiftY2", "vars": {"y": "deltaY2_2"}},
		{"name": "isDynamicYLow", 	"vars": {"y": "deltaYLow"}},
		{"name": "isDynamicYHigh", 	"vars": {"y": "deltaYHigh"}},
		{"name": "isCustomView", 	"vars": {"yaw": "syaw", "pitch": "spitch", "x": "deltaX2_4", "y": "deltaY2_4", "z": "deltaZ2_4"}},
	]
	
	@staticmethod
	def Mode(name): 
		for mode in TuneModes.MODES:
			if mode["name"] == name:
				return mode
		return None

	@staticmethod
	def Index(name): 
		index = -1
		for mode in TuneModes.MODES:
			index = index + 1
			if mode["name"] == name:
				return index
		return index
	
	@staticmethod
	def NextMode(name):
		index = TuneModes.Index(name)
		next_index = (index + 1) % len(TuneModes.MODES)
		return TuneModes.MODES[next_index]

class AppState:
	def __init__(self):
		self.udpSocket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
		self.tuneMode = None
		self.preset = {}
		self.game = {}
		self.trackir = {}
		self.autoCornerEnd   = 140 #126.41
		self.autoCornerStart = 30.0
		self.autoCornerX_end = 140 #170
		self.flaps_control = FlapsManagement(
			open_keys=ingame_flaps_release,
			close_keys=ingame_flaps_retract,
			duration_open=3,
			duration_close=4
		)
		self.checkSix = CheckSixManagement()

	def copy_preset_to_clipboard(self):
		current_preset = self.preset
		# Generate JSON-like string
		preset_str = "{\n"
		for k, v in sorted(current_preset.items()):
			if isinstance(v, float):
				preset_str += '    "{}": {:.4f},\n'.format(k, v)
			else:
				preset_str += '    "{}": {},\n'.format(k, float(v))
		preset_str += "}"
		
		# Copy to clipboard
		set_clipboard_text(preset_str)
 
	def update_y_axis_state(self, center, shift_1, shift_2, shift_dynamic):
		pass
		# """Update Y-axis state flags in the context."""
		self.game["isHeadCenter"] = center
		self.game["isHeadHigh"] = shift_1
		self.game["isHeadHighest"] = shift_2
		self.game["isHeadDynamic"] = shift_dynamic

class IAction(object):
	__metaclass__ = ABCMeta

	@abstractmethod
	def handle(self):
		pass

class ChangePresetAction(IAction):
	def __init__(self, payload = "reset"):
		self._payload = payload
	
	def handle(self, state):
		state.preset = Presets.all.get(self._payload, {})
		speech.say(str(self._payload) + " preset loaded")

class SwitchMode(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.tuneMode = TuneMode() if state.tuneMode == None else None
		speech.say("TuneMode: Please select tune mode") if state.tuneMode != None else speech.say("Game mode")

class SwitchTuneMode(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		mode = state.tuneMode
		if (mode != None):
			if (mode.name == "undefined"):
				state.tuneMode = TuneMode(TuneModes.MODES[0]["name"])
			else:
				state.tuneMode = TuneMode(TuneModes.NextMode(state.tuneMode.name)["name"])
				state.game["centerPendingFrameTimer"] = 5
			speech.say(state.tuneMode.name)

class Tuner(IAction):
	def __init__(self, subject, delta):
		self._subject = subject
		self._delta = delta
	
	def handle(self, state):
		mode = state.tuneMode
		if (mode != None and mode.name != "undefined"):
			state.tuneMode.modify(state, self._subject, self._delta)		

class SwitchSideView(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		isSideView = state.game.get("isSideView", False)
		state.game["isSideView"] = not isSideView

class CenterGlobalView(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.update_y_axis_state(center=False, shift_1=False, shift_2=False, shift_dynamic = False)
		state.game["isZoomIn"] = False
		state.game["isZoomOut"] = False
		state.game["isSideView"] = False
		state.game["centerPendingFrameTimer"] = 5
  
class HeadYCenter(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.update_y_axis_state(center=True, shift_1=False, shift_2=False, shift_dynamic = False)

class HeadYHigh(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.update_y_axis_state(center=False, shift_1=True, shift_2=False, shift_dynamic = False)

class HeadYHighest(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.game["manual_yaw"] = 0
		state.update_y_axis_state(center=False, shift_1=False, shift_2=True, shift_dynamic = False)
  
class HeadYDynamic(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.update_y_axis_state(center=False, shift_1=False, shift_2=False, shift_dynamic = True)

class ZoomIn(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.game["isZoomIn"] = True
		state.game["isZoomOut"] = False

class ZoomOut(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.game["isZoomIn"] = False
		state.game["isZoomOut"] = True

class ZoomMiddle(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.game["isZoomIn"] = False
		state.game["isZoomOut"] = False
  
class FlapsOpen(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.flaps_control.open_flap(current_time = time.time())

class FlapsClose(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.flaps_control.close_flap(current_time = time.time())

class ToggleCheckSixNotification(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		state.checkSix.switchCheckSix()
		speech.say("check six activated") if state.checkSix.isCheckSixActivated else speech.say("check six disabled")

class SwitchCustomView(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		isCustomView = state.game.get("isCustomView", False)
		state.game["isCustomView"] = not isCustomView

class GunView(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		isGunViewAtCenter = state.game.get("isGunViewAtCenter", False)
		state.game["isGunViewAtCenter"] = not isGunViewAtCenter

class CopyToClipboard(IAction):
	def __init__(self):
		pass

	def handle(self, state):
		speech.say("preset copied")
		state.copy_preset_to_clipboard()

class FlapsManagement:
	def __init__(self, open_keys, close_keys, duration_open=3, duration_close=4):
		"""
		Initialize the flaps control logic.

		Args:
			open_keys (list): Keys for opening the flaps.
			close_keys (list): Keys for closing the flaps.
			duration_open (int): Duration for the flaps to stay open.
			duration_close (int): Duration for the flaps to stay closed.
		"""
		self.current_time = None
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
		self.isFlapAction = False

	def _toggle_flap_state(self, is_opening, current_time):
		"""Toggle flap state between opening and closing."""
		self.flap_flags["flap_opened"] = is_opening
		self.flap_flags["is_flap_opening"] = is_opening
		self.flap_flags["is_flap_closing"] = not is_opening
		self.flap_flags["flap_open_start_time"] = current_time if is_opening else None
		self.flap_flags["flap_close_start_time"] = current_time if not is_opening else None

	def _handle_flap_action(self, action, current_time):
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
			if self.isFlapAction == False:
				self.isFlapAction = True
				for key in keys:
					keyboard.setKeyDown(key)
		elif self.flap_flags[start_time_key] is not None and (current_time - self.flap_flags[start_time_key]) > duration:
			if self.isFlapAction == True:
				self.isFlapAction = False
				for key in keys:
					keyboard.setKeyUp(key)
				self.flap_flags[action] = False
				self.flap_flags[start_time_key] = None

	def isFlapOpend(self):
		return self.flap_flags["flap_opened"]
 
	def open_flap(self, current_time):
		self.isFlapAction = False
		if self.flap_flags["is_flap_closing"]:
			for key in self.close_keys:
				keyboard.setKeyUp(key)
		if self.flap_flags["is_flap_closing"] or not self.flap_flags["flap_opened"]:
			self._toggle_flap_state(True, current_time)
	
	def close_flap(self, current_time):
		self.isOpening = False
		if self.flap_flags["is_flap_opening"]:
			for key in self.open_keys:
				keyboard.setKeyUp(key)
		self._toggle_flap_state(False, current_time)
	
	def update(self, current_time):
		self._handle_flap_action("is_flap_opening", current_time)
		self._handle_flap_action("is_flap_closing", current_time)
		
		# Voice feedback for flaps
		if (current_time - self.last_speech_time) >= 1.0 and self.flap_flags["flap_opened"]:
			speech.say("Flaps")
			self.last_speech_time = current_time

class CheckSixManagement: 
	def __init__(self):
		self.isCheckSixActivated = False
		self.six_lastSpeechTime = time.time()
		self.last_yaw_six_time  = time.time()
  
	def switchCheckSix(self):
		self.isCheckSixActivated = not self.isCheckSixActivated
		self.last_yaw_six_time  = time.time()
	  
	def update(self, state):
		if abs(state.trackir.get("yaw", 0)) > 140:
			self.last_yaw_six_time = time.time()
		if (
			(time.time() - self.six_lastSpeechTime) >= 2.0 and 
			not state.flaps_control.isFlapOpend() and 
			time.time() - self.last_yaw_six_time > 6
			and self.isCheckSixActivated
		):
			speech.say("six")
			self.six_lastSpeechTime = time.time()

class EventStream:
	@staticmethod
	def actions(state):
		if state.game.get("centerPendingFrameTimer", None) is not None:
			return
  
		for pad_key, preset_name in presets_map.items():
			if keyboard.getKeyDown(Key.UpArrow) and keyboard.getPressed(pad_key):
				yield ChangePresetAction(preset_name)
	
		if keyboard.getKeyDown(Key.UpArrow) and keyboard.getPressed(Key.NumberPadPeriod):
				yield CopyToClipboard()
   
		if isScrollLock() and state.tuneMode == None:
			yield SwitchMode()

		if not isScrollLock() and state.tuneMode != None:
			yield SwitchMode()
   
		if keyboard.getPressed(Key.Insert) and keyboard.getKeyDown(Key.RightControl) and state.tuneMode != None:
			yield SwitchTuneMode()

		if state.tuneMode:
			mode = state.tuneMode.name
			xyz = 0.01 if mode == "isAuto" else 0.05
			xyz = 0.01 if mode == "isCenter" else xyz
			tuner_map = {
				Key.Delete:      ("yaw",   -1, 0.5),
				Key.End:         ("yaw",   +1, 0.5),
				Key.Insert:      ("pitch", -1, 0.5),
				Key.Home:        ("pitch", +1, 0.5),
				Key.LeftArrow:   ("x",     +1, xyz),
				Key.RightArrow:  ("x",     -1, xyz),
				Key.UpArrow:     ("y",     +1, xyz),
				Key.DownArrow:   ("y",     -1, xyz),
				Key.PageUp:      ("z",     +1, xyz),
				Key.PageDown:    ("z",     -1, xyz),
			}
	
			for key, (axis, direction, delta) in tuner_map.items():
				if keyboard.getKeyDown(key) and keyboard.getKeyDown(Key.RightShift):
					yield Tuner(axis, direction * delta)
  
		if isSideView != state.game.get("isSideView", False):
			yield SwitchSideView()
   
		if centerAll:
			yield CenterGlobalView()
		if centerGunView:
			yield GunView()
		if centerVGunView and btn_modifier:
			yield HeadYCenter()
		if head_y_high:
			yield HeadYHigh()
		if head_y_highest:
			yield HeadYHighest()
		if head_y_dynamic and btn_modifier:
			yield HeadYDynamic()
		if zoomOut and btn_modifier:
			yield ZoomOut()
		if zoomIn and btn_modifier:
			yield ZoomIn()
		if zoomCenter:
			yield ZoomMiddle()
		if openFlaps:
			yield FlapsOpen()
		if closeFlaps:
			yield FlapsClose()
		if sixNotifier and btn_modifier:
			yield ToggleCheckSixNotification()
		if isCustomView != state.game.get("isCustomView", False):
			yield SwitchCustomView()
	
class Diagnostics:
	@staticmethod
	def watch(state): 
		diagnostics.watch(state.preset["deltaX1"])
		diagnostics.watch(state.preset["deltaY1"])
		diagnostics.watch(state.preset["deltaZ1"])	

			
		diagnostics.watch(state.preset["deltaX0"])
		diagnostics.watch(state.preset["deltaY0"])

		diagnostics.watch(state.preset["deltaZ2_1"])
		diagnostics.watch(state.preset["deltaZ2_2"])

		diagnostics.watch(state.preset["deltaX2_1"])

		diagnostics.watch(state.preset["deltaY2_1"])

		diagnostics.watch(state.preset["deltaY2_2"])

		diagnostics.watch(state.preset["deltaX2_4"])
		diagnostics.watch(state.preset["deltaY2_4"])
		diagnostics.watch(state.preset["deltaZ2_4"])

		diagnostics.watch(state.preset["deltaYLow"])
		diagnostics.watch(state.preset["deltaYHigh"])
	
		diagnostics.watch(state.preset["syaw"])
		diagnostics.watch(state.preset["spitch"])

class Six_DOF_Calc_Helpers:
	def __init__(self, state):
		self.state = state
  
	def _compute_manual_x(self, yaw):
		if not self.state.game.get("isSideView", False):
			return 0
		mirror_x_boundary = self.state.autoCornerX_end
		x = self.state.preset["deltaX2_1"]

		if -abs(mirror_x_boundary) <= yaw <= 0:
			return -abs(x)
		elif 0 < yaw < abs(mirror_x_boundary):
			return abs(x)
		elif yaw < -abs(mirror_x_boundary):
			return abs(x * 3)
		elif abs(mirror_x_boundary) <= yaw:
			return -abs(x * 3)
		return 0

	def _compute_manual_y(self, joy_y):
		"""Update deltaY based on the current Y-axis state."""
		if self.state.game.get("isHeadCenter", False):
			return self.state.preset.get("deltaY0", 0)
		if self.state.game.get("isHeadHigh", False):
			return self.state.preset.get("deltaY2_1", 0)
		if self.state.game.get("isHeadHighest", False):
			return self.state.preset.get("deltaY2_2", 0)
		if self.state.game.get("isHeadDynamic", False):
			return filters.ensureMapRange(joy_y, 0, 1000, self.state.preset.get("deltaYLow", 0), self.state.preset.get("deltaYHigh", 0))
		return 0
 
	def ensureMapRange(self, value, input_min, input_max, output_min, output_max):
		# Avoid division by zero
		if input_max - input_min == 0:
			return output_min
		
		# Linear interpolation
		normalized = (value - input_min) / (input_max - input_min)
		return output_min + normalized * (output_max - output_min)

	def _compute_auto_xyz(self, yaw):
		autoX, autoY, autoZ = 0, 0, 0
		if yaw != 0:
			delta_x1 = self.state.preset["deltaX1"]
			delta_y1 = self.state.preset["deltaY1"]
			delta_z1 = self.state.preset["deltaZ1"]
			if abs(yaw) <= self.state.autoCornerStart:
				return autoX, autoY, autoZ
			autoX = self.ensureMapRange(yaw, self.state.autoCornerStart, self.state.autoCornerEnd, 0, delta_x1)
			autoY = self.ensureMapRange(abs(yaw), self.state.autoCornerStart, self.state.autoCornerEnd, 0, delta_y1)
			autoZ = self.ensureMapRange(abs(yaw), self.state.autoCornerStart, self.state.autoCornerEnd, 0, delta_z1)
		return autoX, autoY, autoZ

	def _compute_fake_xyz(self, yaw, pitch, roll, x, y, z, autoX, autoY, autoZ, deltaX, deltaY, deltaZ):
		is_y_on = abs(yaw) >= 45
  
		fake_yaw = yaw * 0.1
		fake_pitch = pitch
		fake_roll = roll
  
		# calculate x
		fake_temp_x = x * (abs(autoX) == 0) + (autoX * 1.0)
		is_in_dead_zone = (abs(fake_temp_x) < 1 and abs(autoX) == 0)
		def apply_dead_zone(value, threshold):
			return value if abs(value) >= threshold or self.state.tuneMode != None else 0 
		x_direction = apply_dead_zone(fake_temp_x, 1) #(fake_temp_x - fake_temp_x / fake_temp_x_divider)
		fake_x = x_direction + deltaX + self.state.preset["deltaX0"] * self.state.game.get("isGunViewAtCenter", False)
		
		# calculate y
		fake_y = (
			y * is_y_on
			+ deltaY
			+ (autoY * 1.0)
			+ self.state.preset["deltaY0"] * self.state.game.get("isGunViewAtCenter", False)
			- self.state.game.get("y_offset", 0) * is_y_on
		)
  
		fake_z = z + deltaZ + (autoZ * 1.0)
	
		if not is_y_on:
			self.state.game["y_offset"] = y
	
		return fake_yaw, fake_pitch, 0, fake_x, fake_y, fake_z

	def _sync(self):
		centerPendingFrameTimer = self.state.game.get("centerPendingFrameTimer", None)

		if centerPendingFrameTimer is not None:
			if centerPendingFrameTimer == 0:
				keyboard.setPressed(GlobalCenterKey)
				self.state.game["centerPendingFrameTimer"] = -1
			elif centerPendingFrameTimer == -5:
				self.state.game["centerPendingFrameTimer"] = None
			else:
				self.state.game["centerPendingFrameTimer"] -= 1
			data = struct.pack('<dddddd', 0, 0, 0, 0, 0, 0)
			self.state.udpSocket.sendto(data, (UDP_IP, UDP_PORT))
			return True
		return False

class GameMode:
	def __init__(self, state):
		self.state = state
		self.calc = Six_DOF_Calc_Helpers(self.state)

	def proccessFrame(self): 
		if self.calc._sync():
			return
  
		self.state.flaps_control.update(current_time=time.time())
		self.state.checkSix.update(state=self.state)
		yaw = self.state.trackir.get("yaw", 0)
		pitch = self.state.trackir.get("pitch", 0)
		roll = self.state.trackir.get("roll", 0)
		x = self.state.trackir.get("x", 0)
		y = self.state.trackir.get("y", 0)
		z = self.state.trackir.get("z", 0)
	
		autoX, autoY, autoZ = self.calc._compute_auto_xyz(yaw)
		deltaX = self.calc._compute_manual_x(yaw)
		deltaY = self.calc._compute_manual_y(stick_y)
		deltaZ = (
			self.state.preset["deltaZ2_1"] * self.state.game.get("isZoomIn", False) +
			self.state.preset["deltaZ2_2"] * self.state.game.get("isZoomOut", False)
		)

		if self.state.game.get("isCustomView", False):
			fake_yaw, fake_pitch, fake_roll = self.state.preset["syaw"], self.state.preset["spitch"], 0
			fake_x, fake_y, fake_z = self.state.preset["deltaX2_4"], self.state.preset["deltaY2_4"], self.state.preset["deltaZ2_4"]
		else:
			fake_yaw, fake_pitch, fake_roll, fake_x, fake_y, fake_z = self.calc._compute_fake_xyz(
				yaw, pitch, roll, x, y, z, autoX, autoY, autoZ, deltaX, deltaY, deltaZ
			)
  
		data = struct.pack('<dddddd', fake_x, fake_y, fake_z, fake_yaw, fake_pitch, fake_roll)
		self.state.udpSocket.sendto(data, (UDP_IP, UDP_PORT))

class TuningMode:
	def __init__(self, state):
		self.state = state
		self.calc = Six_DOF_Calc_Helpers(self.state)
  
	def proccessFrame(self): 
		if self.calc._sync():
			return

		mode = self.state.tuneMode.name
  
		yaw = self.state.preset.get("manual_yaw", 0) if mode == "isAuto" else 0
		
		pitch, roll, x, y, z = 0, 0, 0, 0, 0

		autoX, autoY, autoZ = self.calc._compute_auto_xyz(yaw) if mode == "isAuto" else (0, 0, 0)
		
		self.state.game["isSideView"] = mode == "isManualShiftX"
		self.state.game["isGunViewAtCenter"] = True
		self.state.update_y_axis_state(
	  		center=(mode == "isCenter"), 
			shift_1=(mode == "isManualShiftY1"), 
		 	shift_2=(mode == "isManualShiftY2"), 
		  	shift_dynamic=(mode == "isDynamicYLow" or mode == "isDynamicYHigh"))

		joy_y = 1000 if mode == "isDynamicYHigh" else 0
		
		deltaX = self.calc._compute_manual_x(yaw)
		deltaY = self.calc._compute_manual_y(joy_y)
		deltaZ = (
			self.state.preset["deltaZ2_1"] * (mode == "isZoomIn") +
			self.state.preset["deltaZ2_2"] * (mode == "isZoomOut" or mode == "isCenter" or mode == "isManualShiftY1" or mode == "isManualShiftY2")
		)

		if mode == "isCustomView":
			fake_yaw, fake_pitch, fake_roll = self.state.preset["syaw"], self.state.preset["spitch"], 0
			fake_x, fake_y, fake_z = self.state.preset["deltaX2_4"], self.state.preset["deltaY2_4"], self.state.preset["deltaZ2_4"]
		else:
			fake_yaw, fake_pitch, fake_roll, fake_x, fake_y, fake_z = self.calc._compute_fake_xyz(
				yaw, pitch, roll, x, y, z, autoX, autoY, autoZ, deltaX, deltaY, deltaZ
			)
  
		data = struct.pack('<dddddd', fake_x, fake_y, fake_z, fake_yaw, fake_pitch, fake_roll)
		self.state.udpSocket.sendto(data, (UDP_IP, UDP_PORT))

class Application:
	def __init__(self):
		self.state = AppState()
		def fromTrackIR():
			self.state.trackir["yaw"] = trackIR.yaw
			self.state.trackir["pitch"] = trackIR.pitch
			self.state.trackir["roll"] = trackIR.roll
			self.state.trackir["x"] = trackIR.x
			self.state.trackir["y"] = trackIR.y
			self.state.trackir["x"] = trackIR.z
		trackIR.update += fromTrackIR
		self.tuneMode = TuningMode(self.state)
		self.gameMode = GameMode(self.state)
		(ChangePresetAction()).handle(self.state)
 
	def proccessFrame(self): 
		self.tuneMode.proccessFrame() if self.state.tuneMode != None else self.gameMode.proccessFrame()
		for action in EventStream.actions(self.state): action.handle(self.state)
		Diagnostics.watch(self.state)
  
if starting:
	app = Application()
app.proccessFrame()