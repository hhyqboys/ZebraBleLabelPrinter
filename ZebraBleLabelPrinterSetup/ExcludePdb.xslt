<?xml version="1.0" encoding="utf-8"?>
<!--
  用于 heat.exe 目录收集后的 XSLT 过滤：
  删除 File 文件路径以 .pdb 结尾的整个 Component（即排除调试符号文件，不进 MSI）。
  由 ElectricControlSetup.wixproj 中 HarvestDirectory 的 Transforms 元数据引用。

  注意：heat 5.x 目录收集生成的 File 元素不含 Name 属性，
  文件名体现在 Source="SourceDir\xxx.pdb" 中，因此按 Source 结尾匹配。
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">
  <xsl:output method="xml" indent="yes" />

  <!-- 默认：原样复制所有节点和属性 -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!-- 删除 File 路径以 .pdb 结尾的整个 Component（同时兼容有 Name 属性的情况） -->
  <xsl:template match="wix:Component[wix:File/@Source[substring(., string-length(.) - 3) = '.pdb'] or wix:File/@Name[substring(., string-length(.) - 3) = '.pdb']]" />
</xsl:stylesheet>
