export function SkylineLineArt(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 800 300"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden
      {...props}
    >
      {/* sky birds */}
      <path d="M120 44q6-8 12 0q6-8 12 0" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.25" strokeLinecap="round" />
      <path d="M600 30q5-7 10 0q5-7 10 0" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1.25" strokeLinecap="round" />

      {/* distant back-layer skyline for density */}
      {[
        [0, 200, 26],
        [30, 214, 22],
        [96, 196, 20],
        [200, 210, 24],
        [322, 190, 18],
        [402, 216, 26],
        [512, 200, 22],
        [598, 176, 20],
        [700, 206, 24],
        [750, 192, 20],
        [780, 218, 20],
      ].map(([x, y, w]) => (
        <rect
          key={x}
          x={x}
          y={y}
          width={w}
          height={260 - y}
          stroke="currentColor"
          strokeOpacity="0.14"
          strokeWidth="1"
        />
      ))}

      {/* street baseline */}
      <path d="M0 260H800" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />
      <path d="M0 264H800" stroke="currentColor" strokeOpacity="0.14" strokeWidth="1" strokeDasharray="2 6" />

      {/* townhouse pair, far left */}
      <path d="M14 260v-50h34v50" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <path d="M14 210 31 191 48 210" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" strokeLinejoin="round" />
      <path d="M52 260v-38h30v38" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <path d="M52 222 67 206 82 222" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" strokeLinejoin="round" />
      <rect x="24" y="234" width="10" height="14" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />

      {/* tree */}
      <path d="M104 260v-26" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.25" />
      <circle cx="104" cy="222" r="13" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.25" />

      {/* mid block, windowed */}
      <rect x="128" y="140" width="64" height="120" rx="1.5" stroke="currentColor" strokeOpacity="0.45" strokeWidth="1.25" />
      <path
        d="M128 160h64M128 180h64M128 200h64M128 220h64M128 240h64M160 140v120"
        stroke="currentColor"
        strokeOpacity="0.25"
        strokeWidth="1"
      />

      {/* lamp post */}
      <path d="M206 260v-30" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.25" />
      <circle cx="206" cy="226" r="3.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />

      {/* hero tower */}
      <rect x="230" y="66" width="92" height="194" rx="1.5" stroke="currentColor" strokeOpacity="0.75" strokeWidth="1.5" />
      <path d="M276 66V42" stroke="currentColor" strokeOpacity="0.75" strokeWidth="1.5" />
      <circle cx="276" cy="38" r="3.5" stroke="currentColor" strokeOpacity="0.75" strokeWidth="1.25" />
      <path d="M230 96h92" stroke="currentColor" strokeOpacity="0.55" strokeWidth="1" />
      {[118, 140, 162, 184, 206, 228, 250].map((y) => (
        <path key={y} d={`M242 ${y}h18M310 ${y}h18`} stroke="currentColor" strokeOpacity="0.45" strokeWidth="1" />
      ))}
      <path d="M276 96v164" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />

      {/* glass twin tower with peaked atrium roof */}
      <path d="M337 260V128l38-30 38 30v132" stroke="currentColor" strokeOpacity="0.65" strokeWidth="1.5" strokeLinejoin="round" />
      <path d="M356 260V150M394 260V150" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />
      {[150, 174, 198, 222, 246].map((y) => (
        <path key={y} d={`M337 ${y}h76`} stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />
      ))}

      {/* civic dome building */}
      <path d="M424 260v-70h88v70" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <path
        d="M436 190a32 26 0 0 1 64 0"
        stroke="currentColor"
        strokeOpacity="0.4"
        strokeWidth="1.25"
      />
      <path d="M468 164v-16" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <circle cx="468" cy="144" r="2.75" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.1" />
      {[434, 448, 462, 476, 490, 504].map((x) => (
        <path key={x} d={`M${x} 260v-42`} stroke="currentColor" strokeOpacity="0.28" strokeWidth="1" />
      ))}
      <path d="M424 218h88" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />

      {/* slim infill tower */}
      <rect x="608" y="176" width="22" height="84" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.1" />
      <path d="M608 196h22M608 214h22M608 232h22M608 250h22" stroke="currentColor" strokeOpacity="0.22" strokeWidth="1" />

      {/* residential low-rise with balconies */}
      <rect x="528" y="188" width="78" height="72" rx="1.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      {[206, 224, 242].map((y) => (
        <g key={y}>
          <path d={`M528 ${y}h78`} stroke="currentColor" strokeOpacity="0.28" strokeWidth="1" />
          <path d={`M544 ${y}v-10M566 ${y}v-10M588 ${y}v-10`} stroke="currentColor" strokeOpacity="0.22" strokeWidth="1" />
        </g>
      ))}

      {/* construction crane */}
      <path
        d="M654 260V96M654 96h58M696 96v20M634 118l24-22M642 96l-14 12"
        stroke="currentColor"
        strokeOpacity="0.5"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="654" cy="96" r="3" stroke="currentColor" strokeOpacity="0.55" strokeWidth="1.25" />

      {/* far townhome row */}
      <path
        d="M694 260v-30l16-14 16 14v-14l16-14 16 14v-14l16 14v44"
        stroke="currentColor"
        strokeOpacity="0.32"
        strokeWidth="1.1"
        strokeLinejoin="round"
      />

      {/* small end tower, stepped roof */}
      <path
        d="M770 260v-96h10v-14h10v-10h10v120"
        stroke="currentColor"
        strokeOpacity="0.4"
        strokeWidth="1.25"
        strokeLinejoin="round"
      />
    </svg>
  );
}
