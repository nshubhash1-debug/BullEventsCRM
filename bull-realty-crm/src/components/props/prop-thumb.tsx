"use client";

import * as React from "react";
import { ImageOff } from "lucide-react";

import { propPhotoUrl } from "@/lib/props-api";
import { cn } from "@/lib/utils";

/**
 * A prop photograph, with the empty state built in.
 *
 * Twenty-nine of the register's lines came across without a picture, and a
 * broken-image icon in a grid of six hundred tiles reads as a bug rather than
 * as missing data — so a line with no photo gets a deliberate placeholder.
 */
export function PropThumb({
  src,
  alt,
  className,
  rounded = "rounded-md",
}: {
  src: string | null | undefined;
  alt: string;
  className?: string;
  rounded?: string;
}) {
  // Which URL failed, rather than a bare "it failed" flag: a changed src is a
  // different photograph and deserves its own attempt, and remembering the URL
  // gives it one without an effect resetting the flag on every change.
  const [failedUrl, setFailedUrl] = React.useState<string | null>(null);
  const url = propPhotoUrl(src);

  if (!url || failedUrl === url) {
    return (
      <div
        className={cn(
          "flex items-center justify-center bg-muted text-muted-foreground",
          rounded,
          className
        )}
      >
        <ImageOff className="size-4 opacity-50" />
      </div>
    );
  }

  /*
   * A plain <img>, not next/image: the photographs are served by the API host
   * rather than this app, they are already resized to 320px and 900px on disk,
   * and routing them through the optimiser would only add a hop.
   */
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={url}
      alt={alt}
      loading="lazy"
      onError={() => setFailedUrl(url)}
      className={cn("bg-muted object-cover", rounded, className)}
    />
  );
}
