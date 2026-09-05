"use client";

import * as React from "react";

interface TypewriterLoopOptions {
  typingSpeed?: number;
  deletingSpeed?: number;
  pauseDuration?: number;
  loopPauseDuration?: number;
}

export function useTypewriterLoop(
  items: readonly string[],
  {
    typingSpeed = 32,
    deletingSpeed = 18,
    pauseDuration = 2200,
    loopPauseDuration = 350,
  }: TypewriterLoopOptions = {}
) {
  const [index, setIndex] = React.useState(0);
  const [text, setText] = React.useState("");
  const [phase, setPhase] = React.useState<"typing" | "deleting">("typing");

  React.useEffect(() => {
    const current = items[index % items.length];
    let timeout: ReturnType<typeof setTimeout>;

    if (phase === "typing") {
      if (text.length < current.length) {
        timeout = setTimeout(
          () => setText(current.slice(0, text.length + 1)),
          typingSpeed
        );
      } else {
        timeout = setTimeout(() => setPhase("deleting"), pauseDuration);
      }
    } else {
      if (text.length > 0) {
        timeout = setTimeout(
          () => setText(current.slice(0, text.length - 1)),
          deletingSpeed
        );
      } else {
        timeout = setTimeout(() => {
          setIndex((i) => (i + 1) % items.length);
          setPhase("typing");
        }, loopPauseDuration);
      }
    }

    return () => clearTimeout(timeout);
  }, [
    text,
    phase,
    index,
    items,
    typingSpeed,
    deletingSpeed,
    pauseDuration,
    loopPauseDuration,
  ]);

  return { text, phase };
}
