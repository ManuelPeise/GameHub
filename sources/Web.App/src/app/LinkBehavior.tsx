import React from "react";
import { Link as RouterLink, type LinkProps as RouterLinkProps } from "react-router";

/**
 * Lets MUI links and buttons with "href" navigate through React Router (no full page reload).
 * External and absolute URLs (http, mailto) stay normal links.
 */
const LinkBehavior = React.forwardRef<HTMLAnchorElement, Omit<RouterLinkProps, "to"> & { href: RouterLinkProps["to"] }>(
  ({ href, ...other }, ref) => {
    if (typeof href === "string" && /^(https?:|mailto:)/.test(href)) {
      return <a ref={ref} href={href} {...other} />;
    }
    return <RouterLink ref={ref} to={href} {...other} />;
  },
);
LinkBehavior.displayName = "LinkBehavior";

export default LinkBehavior;
