# The web image. Stage 1 builds dist/ with pnpm. Stage 2 is unprivileged nginx
# serving dist/ on port 8080 as a non-root user, and proxying /api to the api
# app. Both base images are pinned by digest; Renovate bumps them from roadmap
# phase 12 on. The build context is web/ (platform.yaml).

FROM node:24.22.3-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS build
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /app
# pnpm comes from Corepack, at the version in packageManager of package.json.
# @trislabcompany/framework-ui comes from GitHub Packages, which asks for a token: the build gets it as the
# BuildKit secret `packages` (docker build --secret id=packages,env=PACKAGES_TOKEN). pnpm trusts a token only
# in ~/.npmrc, so the same RUN writes it there, installs and deletes it: no layer keeps it.
COPY package.json pnpm-lock.yaml .npmrc ./
RUN --mount=type=secret,id=packages,env=PACKAGES_TOKEN     corepack enable     && echo "//npm.pkg.github.com/:_authToken=${PACKAGES_TOKEN}" > ~/.npmrc     && pnpm install --frozen-lockfile     && rm ~/.npmrc
COPY . .
RUN pnpm build

FROM nginxinc/nginx-unprivileged:1.30.6-alpine@sha256:f5116ed4606f3eb015dd4a8e28cb9b351476dc0ad4bc6e9f93b19cec130cdf03
# Removes what the app never uses, so the image has less to scan and patch:
# curl, and the dynamic modules nginx doesn't load here. Then creates the
# folders 40-config.sh writes /config.json and the /api upstream into, owned
# by the nginx user.
# Upgrades musl and zlib to the Alpine fixes of CVE-2026-107891 and
# CVE-2026-85091, and nginx, from nginx.org's repository, to 1.30.7, the fix of
# CVE-2026-113425, CVE-2026-118899, CVE-2026-121810, CVE-2026-123007,
# CVE-2026-123633 and CVE-2026-123634, until the base image is rebuilt with
# them; drop these upgrades once Trivy passes without them. nginx upgrades only
# after the modules are deleted: they pin its old version.
USER root
RUN apk upgrade --no-cache musl musl-utils zlib
RUN apk del --no-network curl nginx-module-geoip nginx-module-image-filter nginx-module-njs nginx-module-xslt \
    && apk add --no-cache -X "https://nginx.org/packages/alpine/v$(cut -d. -f1,2 /etc/alpine-release)/main" "nginx>=1.30.7" \
    && install -d -o nginx -g nginx /usr/share/nginx/config /etc/nginx/upstream
USER nginx
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --chmod=755 docker-entrypoint.d/40-config.sh /docker-entrypoint.d/40-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
