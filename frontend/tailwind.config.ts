/** @type {import('tailwindcss').Config} */
export default {
    content: [
        "./index.html",
        "./src/**/*.{js,ts,jsx,tsx}",
    ],
    theme: {
        extend: {
            colors: {
                myCustomBlue: '#0070f3',
                myCustomRed: '#ff2d55',
                myCustomGreen: '#2dd4bf',
            },
        },
    },
    plugins: [],
    darkMode: 'selector',
}
