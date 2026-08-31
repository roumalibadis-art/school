"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function SearchBox({
  autoFocus = false,
  defaultValue = "",
  size = "md",
}: {
  autoFocus?: boolean;
  defaultValue?: string;
  size?: "md" | "lg";
}) {
  const router = useRouter();
  const [value, setValue] = useState(defaultValue);

  return (
    <form
      role="search"
      onSubmit={(e) => {
        e.preventDefault();
        const q = value.trim();
        router.push(q ? `/search?q=${encodeURIComponent(q)}` : "/search");
      }}
      className="flex w-full items-stretch gap-2"
    >
      <input
        type="search"
        name="q"
        value={value}
        autoFocus={autoFocus}
        onChange={(e) => setValue(e.target.value)}
        placeholder="Rechercher un module, un cours ou un examen…"
        aria-label="Recherche"
        className={
          "w-full rounded-md border border-line bg-paper-raised text-ink placeholder:text-ink-soft " +
          "focus-visible:ring-2 focus-visible:ring-brand " +
          (size === "lg" ? "px-4 py-3 text-base" : "px-3 py-2 text-sm")
        }
      />
      <button
        type="submit"
        className={
          "shrink-0 rounded-md bg-brand font-medium text-white hover:bg-brand-strong " +
          (size === "lg" ? "px-5 py-3 text-base" : "px-4 py-2 text-sm")
        }
      >
        Rechercher
      </button>
    </form>
  );
}
