/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  async rewrites() {
    // Proxy the browser-side API calls (search-as-you-type, preview images) to the backend
    // so the frontend never needs a public API origin during development.
    const target = process.env.API_URL ?? "http://localhost:5178";
    return [{ source: "/api/:path*", destination: `${target}/api/:path*` }];
  },
};

export default nextConfig;
