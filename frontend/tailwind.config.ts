import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        // Calm academic palette — deep ink + a single scholarly accent.
        ink: {
          DEFAULT: "#1a2330",
          muted: "#55606f",
          soft: "#8a94a3",
        },
        paper: {
          DEFAULT: "#fbfaf7",
          raised: "#ffffff",
          sunken: "#f2f0ea",
        },
        line: "#e4e1d8",
        brand: {
          DEFAULT: "#1f6f5c",
          strong: "#175646",
          soft: "#e7f1ee",
        },
        premium: {
          DEFAULT: "#a9791f",
          soft: "#f6edda",
        },
      },
      fontFamily: {
        sans: ["var(--font-sans)", "system-ui", "sans-serif"],
        serif: ["var(--font-serif)", "Georgia", "serif"],
      },
      maxWidth: {
        prose: "68ch",
      },
      boxShadow: {
        card: "0 1px 2px rgba(26, 35, 48, 0.04), 0 8px 24px -12px rgba(26, 35, 48, 0.12)",
      },
    },
  },
  plugins: [],
};

export default config;
