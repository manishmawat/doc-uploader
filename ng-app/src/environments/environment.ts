export const environment = {
  apiBaseUrl: 'https://doc-uploader-dev-api.azurewebsites.net',
  // Origins allowed to host this app in an iframe. Empty means "same origin as this app",
  // which is the case when it is deployed under /ng/ on the Static Web App (incl. PR previews).
  hostOrigins: [] as string[],
};
