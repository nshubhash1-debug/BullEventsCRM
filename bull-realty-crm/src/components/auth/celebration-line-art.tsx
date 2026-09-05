/**
 * The sign-in illustration: an event, drawn the night before it happens.
 *
 * It replaced a city skyline, which was the right picture for the property
 * business this was converted from and the wrong one for a company that builds
 * weddings. The scene is what the product is actually about — a mandap under
 * strung lights, a marquee, a stage rig, and chairs set out down an aisle.
 *
 * Line art in `currentColor` so the panel's own palette drives it, and layered
 * by opacity the way the skyline was: about 0.15 for the far silhouettes, 0.45
 * for the marquee and the rig, and 0.75 for the mandap, which is the thing the
 * eye is meant to land on.
 */
export function CelebrationLineArt(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 800 300"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden
      {...props}
    >
      {/* distant canopies, for depth behind everything else */}
      {[10, 70, 214, 254, 528, 620, 700, 762].map((x) => (
        <path
          key={x}
          d={`M${x} 248 L${x + 18} 233 L${x + 36} 248`}
          stroke="currentColor"
          strokeOpacity="0.14"
          strokeWidth="1"
          strokeLinejoin="round"
        />
      ))}

      {/* ground */}
      <path d="M0 260H800" stroke="currentColor" strokeOpacity="0.3" strokeWidth="1" />
      <path
        d="M0 264H800"
        stroke="currentColor"
        strokeOpacity="0.14"
        strokeWidth="1"
        strokeDasharray="2 6"
      />

      {/* ---------------- lighting poles and strung bulbs ---------------- */}

      <path d="M30 260V52" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <circle cx="30" cy="49" r="2.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <path d="M770 260V52" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />
      <circle cx="770" cy="49" r="2.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1.25" />

      {/* the long swag passes above the dome; the two short ones come down to it */}
      <path
        d="M30 62 Q400 168 770 62"
        stroke="currentColor"
        strokeOpacity="0.22"
        strokeWidth="1"
      />
      <path
        d="M30 52 Q170 120 302 168"
        stroke="currentColor"
        strokeOpacity="0.45"
        strokeWidth="1"
      />
      <path
        d="M770 52 Q630 120 498 168"
        stroke="currentColor"
        strokeOpacity="0.45"
        strokeWidth="1"
      />

      {/* bulbs, spaced along the two near swags */}
      {[
        [72, 72],
        [113, 91],
        [154, 109],
        [195, 126],
        [236, 143],
        [276, 158],
        [728, 72],
        [687, 91],
        [646, 109],
        [605, 126],
        [564, 143],
        [524, 158],
      ].map(([cx, cy]) => (
        <circle
          key={`${cx}-${cy}`}
          cx={cx}
          cy={cy}
          r="2.75"
          stroke="currentColor"
          strokeOpacity="0.5"
          strokeWidth="1"
        />
      ))}

      {/* ---------------- marquee, left ---------------- */}

      <path
        d="M52 200 L132 158 L212 200"
        stroke="currentColor"
        strokeOpacity="0.45"
        strokeWidth="1.25"
        strokeLinejoin="round"
      />
      {/* scalloped valance along the eaves */}
      <path
        d="M52 200q10 8 20 0q10 8 20 0q10 8 20 0q10 8 20 0q10 8 20 0q10 8 20 0q10 8 20 0q10 8 20 0"
        stroke="currentColor"
        strokeOpacity="0.3"
        strokeWidth="1"
      />
      <path d="M60 208V260M204 208V260" stroke="currentColor" strokeOpacity="0.45" strokeWidth="1.25" />
      <path d="M132 158V200" stroke="currentColor" strokeOpacity="0.2" strokeWidth="1" />
      {/* guy ropes */}
      <path
        d="M52 200 36 260M212 200 228 260"
        stroke="currentColor"
        strokeOpacity="0.22"
        strokeWidth="1"
      />

      {/* ---------------- the mandap ---------------- */}

      {/* steps */}
      <path
        d="M296 260H504M304 252H496M312 246H488"
        stroke="currentColor"
        strokeOpacity="0.5"
        strokeWidth="1.25"
      />

      {/* pillars */}
      {[310, 366, 426, 482].map((x) => (
        <rect
          key={x}
          x={x}
          y="176"
          width="8"
          height="68"
          stroke="currentColor"
          strokeOpacity="0.75"
          strokeWidth="1.5"
        />
      ))}
      {/* capitals */}
      <path
        d="M305 176h18M361 176h18M421 176h18M477 176h18"
        stroke="currentColor"
        strokeOpacity="0.6"
        strokeWidth="1.25"
      />

      {/* entablature */}
      <path
        d="M300 172H500M300 168H500"
        stroke="currentColor"
        strokeOpacity="0.75"
        strokeWidth="1.5"
      />

      {/* dome, its inner line, and the finial */}
      <path
        d="M304 168 Q400 96 496 168"
        stroke="currentColor"
        strokeOpacity="0.75"
        strokeWidth="1.5"
      />
      <path
        d="M320 168 Q400 116 480 168"
        stroke="currentColor"
        strokeOpacity="0.35"
        strokeWidth="1"
      />
      <path d="M400 132V116" stroke="currentColor" strokeOpacity="0.65" strokeWidth="1.25" />
      <circle cx="400" cy="111" r="5" stroke="currentColor" strokeOpacity="0.65" strokeWidth="1.25" />
      <circle cx="400" cy="101" r="2" stroke="currentColor" strokeOpacity="0.5" strokeWidth="1" />

      {/* drapes slung between the pillars */}
      <path
        d="M318 180 Q342 202 366 180M374 180 Q400 204 426 180M434 180 Q458 202 482 180"
        stroke="currentColor"
        strokeOpacity="0.35"
        strokeWidth="1"
      />

      {/* ---------------- floral urns flanking the steps ---------------- */}

      {[280, 520].map((cx) => (
        <g key={cx}>
          <path
            d={`M${cx - 9} 246 L${cx - 6} 258 H${cx + 6} L${cx + 9} 246 Z`}
            stroke="currentColor"
            strokeOpacity="0.45"
            strokeWidth="1.25"
            strokeLinejoin="round"
          />
          <circle cx={cx} cy="234" r="4.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1" />
          <circle cx={cx - 9} cy="239" r="3.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1" />
          <circle cx={cx + 9} cy="239" r="3.5" stroke="currentColor" strokeOpacity="0.4" strokeWidth="1" />
          <circle cx={cx - 5} cy="227" r="3" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1" />
          <circle cx={cx + 5} cy="227" r="3" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1" />
        </g>
      ))}

      {/* ---------------- stage and lighting rig, right ---------------- */}

      <rect
        x="560"
        y="236"
        width="180"
        height="10"
        stroke="currentColor"
        strokeOpacity="0.45"
        strokeWidth="1.25"
      />
      <path
        d="M566 246v14M650 246v14M734 246v14"
        stroke="currentColor"
        strokeOpacity="0.25"
        strokeWidth="1"
      />
      <path d="M572 236V152M728 236V152" stroke="currentColor" strokeOpacity="0.45" strokeWidth="1.25" />
      <rect
        x="566"
        y="142"
        width="168"
        height="10"
        stroke="currentColor"
        strokeOpacity="0.45"
        strokeWidth="1.25"
      />
      {/* truss lattice */}
      <path
        d="M566 152 586 142 606 152 626 142 646 152 666 142 686 152 706 142 726 152"
        stroke="currentColor"
        strokeOpacity="0.25"
        strokeWidth="1"
        strokeLinejoin="round"
      />
      {/* fixtures, and the light they throw */}
      {[596, 636, 676, 716].map((x) => (
        <g key={x}>
          <path d={`M${x} 152v8`} stroke="currentColor" strokeOpacity="0.4" strokeWidth="1" />
          <path
            d={`M${x - 5} 160 H${x + 5} L${x + 7} 168 H${x - 7} Z`}
            stroke="currentColor"
            strokeOpacity="0.45"
            strokeWidth="1.25"
            strokeLinejoin="round"
          />
          <path
            d={`M${x - 7} 168 L${x - 17} 200M${x + 7} 168 L${x + 17} 200`}
            stroke="currentColor"
            strokeOpacity="0.12"
            strokeWidth="1"
          />
        </g>
      ))}

      {/* ---------------- the aisle, and chairs set out down it ---------------- */}

      <path
        d="M336 300 L372 262M464 300 L428 262"
        stroke="currentColor"
        strokeOpacity="0.2"
        strokeWidth="1"
      />
      <path
        d="M400 264V300"
        stroke="currentColor"
        strokeOpacity="0.12"
        strokeWidth="1"
        strokeDasharray="3 7"
      />

      {[
        90, 116, 142, 168, 194, 220, 246, 272, 298,
        502, 528, 554, 580, 606, 632, 658, 684, 710,
      ].map((cx) => (
        <path
          key={cx}
          d={`M${cx - 5} 272v7h10v-7M${cx - 5} 279v5M${cx + 5} 279v5`}
          stroke="currentColor"
          strokeOpacity="0.32"
          strokeWidth="1"
          strokeLinejoin="round"
        />
      ))}
    </svg>
  );
}
