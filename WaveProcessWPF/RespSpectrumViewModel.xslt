<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                xmlns="http://www.w3.org/1999/xhtml"
                exclude-result-prefixes="msxsl">
  <xsl:output method="html" indent="yes" encoding="utf-8" />

  <xsl:template match="/RespSpectrumViewModel">
    <xsl:text disable-output-escaping="yes"><![CDATA[<!DOCTYPE html>]]></xsl:text>
    <html lang="jp">
      <head>
        <meta charset="utf-8" />
        <title>Result of Response Spectrum</title>
        <link rel="stylesheet" href="bootstrap.min.css" />
      </head>
      <body>
        <div class="container-fluid">
          <h1>地震波</h1>
          <h2>一覧</h2>
          <div class="table-responsive">
            <table class="table table-striped">
              <thead>
                <tr>
                  <th>名前</th>
                  <th>Dt[s]</th>
                  <th>データ数</th>
                  <th>最大値(絶対値)[m/s2]</th>
                  <th>最大値発生時間[s]</th>
                </tr>
              </thead>
              <tbody>
                <xsl:for-each select="//Wave">
                  <tr>
                    <td>
                      <xsl:value-of select="Name" />
                    </td>
                    <td>
                      <xsl:value-of select="Dt" />
                    </td>
                    <td>
                      <xsl:value-of select="count(Data/double)" />
                    </td>
                    <td>
                      <xsl:value-of select="Max" />
                    </td>
                    <td>
                      <xsl:value-of select="MaxTime" />
                    </td>
                  </tr>
                </xsl:for-each>
              </tbody>
            </table>
          </div>
          <h2>波形</h2>
          <xsl:for-each select="/RespSpectrumViewModel/Waves/Wave">
            <figure class="figure">
              <img class="figure-img img-fluid">
                <xsl:attribute name="src">
                  WAVE_<xsl:value-of select="Name"/>.png
                </xsl:attribute>
              </img>
              <figcaption class="figure-caption">
                <xsl:value-of select="Name"/>
              </figcaption>
            </figure>
            <br />
          </xsl:for-each>
          <h1 style="page-break-before: always;">減衰定数</h1>
            <ul class="list-unstyled">
              <xsl:for-each select="/RespSpectrumViewModel/Hs/double">
                <xsl:sort data-type="number" order="ascending"/>
                <li>
                  h=<xsl:value-of select="." />
                </li>
              </xsl:for-each>
            </ul>
          <h1 style="page-break-before: always;">結果</h1>
          <h2>加速度応答スペクトル</h2>
            <xsl:for-each select="/RespSpectrumViewModel/Waves/Wave">
              <figure class="figure">
                <img class="figure-img img-fluid">
                  <xsl:attribute name="src">
                    RespAcc_<xsl:value-of select="Name"/>.png
                  </xsl:attribute>
                </img>
                <figcaption class="figure-caption">
                  <xsl:value-of select="Name"/>
                </figcaption>
              </figure>
              <br />
            </xsl:for-each>
          <h2 style="page-break-before: always;">速度応答スペクトル</h2>
                      <xsl:for-each select="/RespSpectrumViewModel/Waves/Wave">
              <figure class="figure">
                <img class="figure-img img-fluid">
                  <xsl:attribute name="src">
                    RespVel_<xsl:value-of select="Name"/>.png
                  </xsl:attribute>
                </img>
                <figcaption class="figure-caption">
                  <xsl:value-of select="Name"/>
                </figcaption>
              </figure>
              <br />
            </xsl:for-each>
          <h2 style="page-break-before: always;">変位応答スペクトル</h2>
                    <xsl:for-each select="/RespSpectrumViewModel/Waves/Wave">
              <figure class="figure">
                <img class="figure-img img-fluid">
                  <xsl:attribute name="src">
                    RespDis_<xsl:value-of select="Name"/>.png
                  </xsl:attribute>
                </img>
                <figcaption class="figure-caption">
                  <xsl:value-of select="Name"/>
                </figcaption>
              </figure>
              <br />
            </xsl:for-each>
        </div>
        <script src="jquery-3.0.0.slim.min.js" />
        <script src="bootstrap.bundle.min.js" />
      </body>
    </html>
  </xsl:template>

  <xsl:template match="Hs">
    <ul>
      <xsl:for-each select="double">
        <li>
          <xsl:value-of select="." />
        </li>
      </xsl:for-each>
    </ul>
  </xsl:template>

</xsl:stylesheet>
