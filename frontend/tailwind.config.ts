import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          red: "#D92E55",
          soft: "#FCECED",
          pale: "#FFF7F7",
          ink: "#222222",
          muted: "#6F6767",
          line: "#EAD9DA"
        }
      },
      fontFamily: {
        sans: ["Arial", "Helvetica", "sans-serif"],
        serif: ["Georgia", "Times New Roman", "serif"]
      },
      boxShadow: {
        beauty: "0 18px 40px rgba(142, 49, 72, 0.12)"
      }
    }
  },
  plugins: []
};

export default config;
