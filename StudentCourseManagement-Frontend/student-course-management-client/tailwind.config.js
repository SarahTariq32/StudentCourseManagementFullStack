/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        // Main brand colors (Primary buttons, active links, main headers)
        brand: {
          50: '#eef2ff',
          100: '#e0e7ff',
          500: '#6366f1', // Primary accent (Indigo)
          600: '#4f46e5', // Primary hover state
          700: '#4338ca',
          900: '#1e1b4b', // Dark headers / Branding text
        },
        // Deep Slate (Sidebars, dark card headers, crisp dark text)
        dark: {
          800: '#0f172a',
          900: '#020617',
        },
        // Accent state colors (Status badges, notifications, error states)
        accent: {
          emerald: '#10b981',
          rose: '#f43f5e',
          amber: '#f59e0b',
        }
      }
    },
  },
  plugins: [],
}