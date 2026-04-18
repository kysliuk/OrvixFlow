import { auth } from "@/auth"

export default auth((req) => {
  const isLoggedIn = !!(req.auth && (req.auth as any).apiToken);
  const { pathname } = req.nextUrl;

  // Public routes that don't require authentication
  const publicRoutes = ["/login", "/register", "/api/auth", "/verify", "/api/verify"];

  const isPublicRoute = publicRoutes.some(route => pathname.startsWith(route));

  if (!isLoggedIn && !isPublicRoute) {
    // Redirect unauthenticated users to login
    return Response.redirect(new URL("/login", req.nextUrl));
  }

  // Server-side role check for admin routes (F-25)
  if (isLoggedIn && pathname.startsWith("/admin")) {
    const role = (req.auth?.user as any)?.globalRole || req.auth?.user?.role;
    const isSuperAdmin = role === "SuperAdmin" || role === "InternalOperator";
    if (!isSuperAdmin) {
      // Redirect non-admins away from admin pages
      return Response.redirect(new URL("/", req.nextUrl));
    }
  }

  if (isLoggedIn && (pathname === "/login" || pathname === "/register")) {
    // Redirect authenticated users away from auth pages
    return Response.redirect(new URL("/", req.nextUrl));
  }
})

// Optionally, don't invoke Middleware on some paths
export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
}
