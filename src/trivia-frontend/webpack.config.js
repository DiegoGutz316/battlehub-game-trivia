/* eslint-disable @typescript-eslint/no-var-requires */
// ADR-003 — Configuración de Webpack para el microfrontend de un juego (remote).
// Basada en la plantilla adrs/plantillas/ADR-003/juego-remote de battlehub-contracts.

// ===================== DATOS DEL EQUIPO =====================
// Equipo 5 (Trivia), según la tabla de la sección 2 del ADR-003.
const REMOTE_NAME = 'triviaGame';
const PORT = 4002;
// ============================================================

const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const Dotenv = require('dotenv-webpack');
const { ModuleFederationPlugin } = require('webpack').container;
const sharedDeps = require('./mf-shared');
const { DefinePlugin } = require('webpack');

module.exports = function (env) {
  const production = env.production || process.env.NODE_ENV === 'production';
  return {
    target: 'web',
    mode: production ? 'production' : 'development',
    devtool: production ? undefined : 'eval-source-map',
    entry: { entry: './src/main.ts' },
    output: {
      clean: true,
      path: path.resolve(__dirname, 'dist'),
      filename: production ? '[name].[contenthash].bundle.js' : '[name].bundle.js',
      publicPath: 'auto',          // ADR-003 §8: los chunks se piden a ESTE servidor, no al del Shell
      uniqueName: REMOTE_NAME,
    },
    resolve: {
      extensions: ['.ts', '.js'],
      modules: [path.resolve(__dirname, 'src'), 'node_modules'],
      // Sin alias de desarrollo: se deben resolver los mismos paquetes que se comparten.
    },
    devServer: {
      historyApiFallback: true,
      open: !process.env.CI,
      port: PORT,
      headers: { 'Access-Control-Allow-Origin': '*' }, // ADR-003 §8: CORS para que el Shell descargue remoteEntry.js
    },
    performance: { hints: false },
    module: {
      rules: [
        { test: /\.(png|svg|jpg|jpeg|gif)$/i, type: 'asset' },
        { test: /\.(woff|woff2|ttf|eot|svg|otf)(\?v=[0-9]\.[0-9]\.[0-9])?$/i, type: 'asset' },
        { test: /\.css$/i, use: ['style-loader', 'css-loader'] },
        { test: /\.ts$/i, use: ['ts-loader', '@aurelia/webpack-loader'], exclude: /node_modules/ },
        { test: /[/\\]src[/\\].+\.html$/i, use: '@aurelia/webpack-loader', exclude: /node_modules/ },
      ],
    },
    plugins: [
      new DefinePlugin({ 'process.env.TRIVIA_API_URL': JSON.stringify(process.env.TRIVIA_API_URL || 'http://localhost:5185') }),
      new ModuleFederationPlugin({
        name: REMOTE_NAME,
        filename: 'remoteEntry.js',                          // ADR-003 §2
        exposes: { './GameModule': './src/game-module' },   // ADR-003 §2
        shared: sharedDeps,                                  // ADR-003 §3
      }),
      new HtmlWebpackPlugin({ template: 'index.html', favicon: 'favicon.ico' }),
      new Dotenv({ path: `./.env${production ? '' : '.' + (process.env.NODE_ENV || 'development')}`, silent: true }),
    ],
  };
};
