FROM node:latest

COPY package.json ./

RUN npm install

COPY emailConfig.json ./

COPY index.js ./

CMD [ "node", "index.js" ]