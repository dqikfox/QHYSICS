<h1>Project Faraday: Circuit Simulation in Virtual Reality</h1>

[<img src="https://img.youtube.com/vi/rXlC2jIwGBw/maxresdefault.jpg" width="100%">](https://youtu.be/rXlC2jIwGBw)

Project Faraday is an experiment in virtual reality education, allowing anyone with a VR headset to explore basic circuit design and develop an intuition for the way electricity behaves in the real world. Beginners of all ages can benefit from the ability to combine any number of wires, motors, bulbs and switches to construct arbitrarily complex experiments. Unlike traditional classroom education, virtual reality allows the student to actually see electron flow and its effects on various components in an accessible and fun environment, hopefully encouraging natural exploration and hands-on learning. For a brief demonstration, <a href="https://youtu.be/rXlC2jIwGBw">click here.</a>

<h2>Technology</h2>

This project uses the Unity game engine, Unity's XR integration framework, and <a href="https://spicesharp.github.io/SpiceSharp/index.html">SpiceSharp</a>, a freely available open source circuit simulation library. The code has been developed and tested extensively on the Meta(Oculus) Rift headset and Touch controllers, but because it uses Unity's XR framework rather than proprietary Meta APIs, it can be easily adapted to other headsets and controllers as well.

<h2>Features</h2>

<ul>
  <li>Unlimited supply of circuit components, including batteries, switches, bulbs, motors, and wires</li>
  <li>Components snap to grid when dropped for fool-proof circuit creation 
  <li>Audio feedback and current flow visualization whenever a valid circuit is completed</li>
  <li>Short circuit detection with visual and auditory feedback, indicating the exact components involved</li>
  <li>Adaptive components - motors change speed and bulbs change intensity based on level of current</li>
  <li>Interactive components - bulbs change color and switches open/close when pinched</li>
  <li>Label lever activates current, resistance, and voltage drop labels on active circuits</li>
  <li>Reset lever sends all components back to their dispensers for easy cleanup</li>
  <li>Table height can be easily adjusted by grabbing front bar for seated or standing play</li>
  <li>Teleport locomotion with controller button as well as smooth locomotion and snap-turning with thumbstick</li>
  <li>Relaxing mountain meadow environment makes for a serene learning experience</li>
</ul>
  
<h2>Author</h2>

All code outside of the SpiceSharp library was designed, written, and tested by <a href="https://www.linkedin.com/in/dschack/">Darren Schack</a>, a Seattle-based full stack software engineer with a passion for technology and a particular interest in virtual reality.

<h2>Reality Engine VERSION 0.2 - OpenXR foundation (Quest 3S)</h2>

<p>Faraday now uses Unity OpenXR instead of the deprecated Oculus XR Plugin. Gameplay, SpiceSharp, and the Faraday.unity circuit lab were not rewritten. Faraday scripts are Unity XR / XRI based (no OVRInput / OVRManager / UnityEngine.XR.Oculus hits).</p>

<p><b>Play in Editor (Quest 3S via Link)</b></p>
<ol>
  <li>Open <code>Assets/Scenes/Faraday.unity</code></li>
  <li>Edit &gt; Project Settings &gt; XR Plug-in Management: check <b>OpenXR</b> on the <b>Windows</b> tab and the <b>Android</b> tab. Uncheck Oculus if it still appears.</li>
  <li>Optional: Unity menu <b>Reality Engine &gt; Enable OpenXR for Quest</b> assigns the OpenXR loader and removes Oculus if still assigned.</li>
  <li>In the Game view, turn <b>Gizmos</b> off.</li>
  <li>Quest 3S via Link, then Play.</li>
</ol>

<p><b>Android / Quest device build</b> - Player Settings &gt; Android:</p>
<ul>
  <li>Scripting Backend: IL2CPP</li>
  <li>Target Architectures: ARM64</li>
  <li>Minimum API Level: 29</li>
  <li>Texture compression: ASTC</li>
</ul>

<p>OpenXR Plugin is the Unity 6 Package Manager pin already in this project (<code>com.unity.xr.openxr</code> 1.18.0). Meta's 2026-05-18 note recommended 1.15.1; that version's package does not list Unity 6 (<code>unity: 6000.0</code> starts at 1.18.0 here). <code>com.unity.xr.oculus</code> has been removed.</p>
