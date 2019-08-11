#region license

//  Copyright (C) 2019 ClassicUO Development Community on Github
//
//	This project is an alternative client for the game Ultima Online.
//	The goal of this is to develop a lightweight client considering 
//	new technologies.  
//      
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Linq;
using System.Text;

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Map;
using ClassicUO.IO;
using ClassicUO.IO.Resources;
using ClassicUO.Utility;
using ClassicUO.Renderer;

using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    internal partial class GameScene
    {
        private sbyte _maxGroundZ;
        private int _maxZ;
        private bool _noDrawRoofs;
        private int _objectHandlesCount;
        //private WeakReference<GameObject>[] _renderList = new WeakReference<GameObject>[2000];


        private Point _offset, _maxTile, _minTile, _minPixel, _maxPixel;
        private int _oldPlayerX, _oldPlayerY, _oldPlayerZ;

        private int _renderIndex = 1;

        private GameObject[] _renderList = new GameObject[2000];
        private int _renderListCount;

        public void UpdateMaxDrawZ(bool force = false)
        {
            int playerX = World.Player.X;
            int playerY = World.Player.Y;
            int playerZ = World.Player.Z;

            if (playerX == _oldPlayerX && playerY == _oldPlayerY && playerZ == _oldPlayerZ && !force)
                return;

            _oldPlayerX = playerX;
            _oldPlayerY = playerY;
            _oldPlayerZ = playerZ;

            sbyte maxGroundZ = 127;
            _maxGroundZ = 127;
            _maxZ = 127;
            _noDrawRoofs = !Engine.Profile.Current.DrawRoofs;
            int bx = playerX;
            int by = playerY;
            Tile tile = World.Map.GetTile(bx, by, false);

            if (tile != null)
            {
                int pz14 = playerZ + 14;
                int pz16 = playerZ + 16;

                GameObject obj = tile.FirstNode;

                while (obj.Left != null)
                    obj = obj.Left;

                for (; obj != null; obj = obj.Right)
                {
                    sbyte tileZ = obj.Z;

                    if (obj is Land)
                    {
                        if (pz16 <= tileZ)
                        {
                            maxGroundZ = (sbyte) pz16;
                            _maxGroundZ = (sbyte) pz16;
                            _maxZ = _maxGroundZ;

                            break;
                        }

                        continue;
                    }

                    if (obj is Mobile)
                        continue;


                    //if (obj is Item it && !it.ItemData.IsRoof || !(obj is Static) && !(obj is Multi))
                    //    continue;

                    if (tileZ > pz14 && _maxZ > tileZ)
                    {
                        if (GameObjectHelper.TryGetStaticData(obj, out var itemdata) && ((ulong) itemdata.Flags & 0x20004) == 0 && (!itemdata.IsRoof || itemdata.IsSurface))
                        {
                            _maxZ = tileZ;
                            _noDrawRoofs = true;
                        }
                    }
                }

                int tempZ = _maxZ;
                _maxGroundZ = (sbyte) _maxZ;
                playerX++;
                playerY++;
                bx = playerX;
                by = playerY;
                tile = World.Map.GetTile(bx, by, false);

                if (tile != null)
                {
                    GameObject obj2 = tile.FirstNode;

                    while (obj2.Left != null)
                        obj2 = obj2.Left;

                    for (; obj2 != null; obj2 = obj2.Right)
                    {
                        //if (obj is Item it && !it.ItemData.IsRoof || !(obj is Static) && !(obj is Multi))
                        //    continue;

                        if (obj2 is Mobile)
                            continue;

                        sbyte tileZ = obj2.Z;

                        if (tileZ > pz14 && _maxZ > tileZ)
                        {
                            if (GameObjectHelper.TryGetStaticData(obj2, out var itemdata) && ((ulong) itemdata.Flags & 0x204) == 0 && itemdata.IsRoof)
                            {
                                _maxZ = tileZ;
                                World.Map.ClearBockAccess();
                                _maxGroundZ = World.Map.CalculateNearZ(tileZ, playerX, playerY, tileZ);
                                _noDrawRoofs = true;
                            }
                        }
                    }

                    tempZ = _maxGroundZ;
                }

                _maxZ = _maxGroundZ;

                if (tempZ < pz16)
                {
                    _maxZ = pz16;
                    _maxGroundZ = (sbyte) pz16;
                }

                _maxGroundZ = maxGroundZ;
            }
        }

        private static readonly StaticTiles _emptyStaticTiles = default;

        private byte AddTileToRenderList(GameObject obj, int worldX, int worldY, bool useObjectHandles, int maxZ, ushort hue = 20, byte extra = 0xFF)
        {
            /*sbyte HeightChecks = 0;
            if(entity != null)
            {
                if(entity.X < worldX && entity.Y > worldY)
                {
                    HeightChecks = 1;
                }
                else if (entity.Y < worldY && entity.X > worldX)
                {
                    HeightChecks = -1;
                }
            }*/

            byte result = 0xFF;

            for (; obj != null; obj = obj.Right)
            {
                if (obj.CurrentRenderIndex == _renderIndex || !obj.AllowedToDraw)
                    continue;

                if (UpdateDrawPosition && obj.CurrentRenderIndex != _renderIndex || obj.IsPositionChanged)
                    obj.UpdateRealScreenPosition(_offset.X, _offset.Y);

                obj.UseInRender = 0xFF;

                int drawX = obj.RealScreenPosition.X;
                int drawY = obj.RealScreenPosition.Y;

                if (drawX < _minPixel.X || drawX > _maxPixel.X)
                    break;

                int maxObjectZ = obj.PriorityZ;


                StaticTiles itemData = _emptyStaticTiles;

                bool changinAlpha = false;
                bool island = false;
                bool iscorpse = false;
                bool ismobile = false;

                switch (obj)
                {
                    case Mobile _:

                        if (extra != 0xFF)
                        {
                            result = 0;

                            return result;
                        }

                        maxObjectZ += Constants.DEFAULT_CHARACTER_HEIGHT;
                        ismobile = true;

                        break;

                    case Land _:
                        island = true;
                        goto SKIP_HANDLES_CHECK;

                    case Item it when it.IsCorpse:
                        iscorpse = true;
                        goto default;

                    default:

                        if (GameObjectHelper.TryGetStaticData(obj, out itemData))
                        {
                            if (itemData.IsFoliage && World.Season >= Seasons.Winter)
                            {
                                continue;
                            }

                            if (obj is Static st)
                            {
                                if (StaticFilters.IsTree(st.OriginalGraphic))
                                {
                                    if (Engine.Profile.Current.TreeToStumps && st.Graphic != Constants.TREE_REPLACE_GRAPHIC)
                                        st.SetGraphic(Constants.TREE_REPLACE_GRAPHIC);
                                    else if (st.OriginalGraphic != st.Graphic && !Engine.Profile.Current.TreeToStumps)
                                        st.RestoreOriginalGraphic();
                                }
                            }

                            if (_noDrawRoofs && itemData.IsRoof)
                            {
                                if (_alphaChanged)
                                    changinAlpha = obj.ProcessAlpha(0);
                                else
                                    changinAlpha = obj.AlphaHue != 0;

                                if (!changinAlpha)
                                    continue;
                            }

                            if (Engine.Profile.Current.TreeToStumps && itemData.IsFoliage || Engine.Profile.Current.HideVegetation && StaticFilters.IsVegetation(obj.Graphic))
                                continue;

                            //if (HeightChecks <= 0 && (!itemData.IsBridge || ((itemData.Flags & TileFlag.StairBack | TileFlag.StairRight) != 0) || itemData.IsWall))

                            //if (!itemData.IsSurface || 

                            //    ( (extra == 12 || extra == 17) && ((itemData.Flags & TileFlag.StairRight) != 0 || 
                            //                                      (itemData.Flags & TileFlag.StairBack) != 0) )
                            //    )


                            //if ((extra == 8 || extra == 15 || extra == 21 ||
                            //          extra == 1 || extra == 2 || extra == 9 || extra == 16 ||
                            //          extra == 3 || extra == 10 ||
                            //          extra == 8 ||
                            //          extra == 7 ||
                            //          extra == 14 || 
                            //          extra == 2) && ((itemData.Flags & TileFlag.StairRight) != 0 ||
                            //                      (itemData.Flags & TileFlag.StairBack) != 0 || itemData.IsSurface || (itemData.IsWall && itemData.Height < 10)))
                            //{
                            //    break;
                            //}

                            {
                                int height = itemData.Height;

                                if (height == 0 && obj.Texture != null && obj.Texture.Height >= 80)
                                    height = 20;

                                //if ((extra == 8 || extra == 15 || extra == 21 ||
                                //     extra == 1 || extra == 2 || extra == 9 || extra == 16 ||
                                //     extra == 3 || extra == 10))
                                //    maxObjectZ -= 20;

                                maxObjectZ += height;
                            }
                        }

                        break;
                }


                if (useObjectHandles && NameOverHeadManager.IsAllowed(obj as Entity))
                {
                    obj.UseObjectHandles = (ismobile ||
                                            iscorpse ||
                                            obj is Item it && (!it.IsLocked || it.IsLocked && itemData.IsContainer) && !it.IsMulti) &&
                                           !obj.ClosedObjectHandles && _objectHandlesCount <= 400;
                    if (obj.UseObjectHandles)
                        _objectHandlesCount++;
                }
                else if (obj.ClosedObjectHandles)
                {
                    obj.ClosedObjectHandles = false;
                    obj.ObjectHandlesOpened = false;
                }
                else if (obj.UseObjectHandles)
                {
                    obj.ObjectHandlesOpened = false;
                    obj.UseObjectHandles = false;
                }


                SKIP_HANDLES_CHECK:

                if (maxObjectZ > maxZ)
                {
                    break;
                }

                obj.CurrentRenderIndex = _renderIndex;

                if (!island)
                    obj.UpdateTextCoords();
                else
                    goto SKIP_INTERNAL_CHECK;

                if (!ismobile && !iscorpse && !island && itemData.IsInternal)
                    continue;

                SKIP_INTERNAL_CHECK:

                int z = obj.Z;

                if (!island && z >= _maxZ)
                {
                    if (!changinAlpha)
                    {
                        if (_alphaChanged)
                            changinAlpha = obj.ProcessAlpha(0);
                        else
                            changinAlpha = obj.AlphaHue != 0;

                        if (!changinAlpha)
                        {
                            obj.UseInRender = (byte)_renderIndex;
                            continue;
                        }
                    }
                }

                int testMaxZ = drawY;

                if (testMaxZ > _maxPixel.Y)
                {
                    continue;
                }

                int testMinZ = drawY + (z << 2);

                if (island)
                {
                    Land t = obj as Land;

                    if (t.IsStretched)
                        testMinZ -= t.MinZ << 2;
                    else
                        testMinZ = testMaxZ;
                }
                else
                    testMinZ = testMaxZ;

                if (testMinZ < _minPixel.Y)
                {
                    continue;
                }


                if (obj.Hue >= 0x35)
                    hue += 20;

                obj.Hue = hue;

                if (ismobile || iscorpse)
                {
                    AddOffsetCharacterTileToRenderList(obj, useObjectHandles);
                }
                else if (!island && itemData.IsFoliage)
                {
                    bool check = World.Player.X <= worldX && World.Player.Y <= worldY;

                    if (!check)
                    {
                        check = World.Player.Y <= worldY && World.Player.X <= worldX + 1;

                        if (!check)
                            check = World.Player.X <= worldX && World.Player.Y <= worldY + 1;
                    }

                    if (check)
                    {
                        _rectangleObj.X = drawX - obj.FrameInfo.X;
                        _rectangleObj.Y = drawY - obj.FrameInfo.Y;
                        _rectangleObj.Width = obj.FrameInfo.Width;
                        _rectangleObj.Height = obj.FrameInfo.Height;

                        check = Exstentions.InRect(ref _rectangleObj, ref _rectanglePlayer);
                    }

                    switch (obj)
                    {
                        case Static st:
                            st.CharacterIsBehindFoliage = check;

                            break;

                        case Multi m:
                            m.CharacterIsBehindFoliage = check;

                            break;

                        case Item it:
                            it.CharacterIsBehindFoliage = check;

                            break;
                    }
                }

                if (!island && _alphaChanged && !changinAlpha)
                {
                    if (itemData.IsTranslucent)
                        obj.ProcessAlpha(178);
                    else if (!itemData.IsFoliage && obj.AlphaHue != 0xFF)
                        obj.ProcessAlpha(0xFF);
                }

                if (_renderListCount >= _renderList.Length)
                {
                    int newsize = _renderList.Length + 1000;
                    Array.Resize(ref _renderList, newsize);
                }
               
                _renderList[_renderListCount++] = obj;
                obj.UseInRender = (byte)_renderIndex;
            }

            return result;
        }


        private void AddOffsetCharacterTileToRenderList(GameObject entity, bool useObjectHandles)
        {
            int charX = entity.X;
            int charY = entity.Y;
            int maxZ = entity.PriorityZ;
            int dropMaxZIndex = -1, area = 0;

            if(entity.FrameInfo != Rectangle.Empty)
            {
                area = Math.Max((entity.FrameInfo.Bottom / 44) >> 1, (entity.FrameInfo.Right / 44) >> 1);
                if(entity is Mobile m && m.IsMoving)
                {
                    byte b = m.Steps.Back().Direction;
                    if (area > 1 && b > 0 && b < 6)
                        area++;
                    else if(b == 7)
                        area--;
                }
            }

            for (byte i = 0; i < 22; i++)
            {
                ushort hue = 0x35;

                int x = charX;
                int y = charY;

                switch (i)
                {
                    case 0://mandatory
                        x++;
                        y--;
                        dropMaxZIndex = 0;
                        break;
                    case 1:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        x++;
                        y -= 2;
                        break;
                    case 2:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        x += 2;
                        y -= 2;
                        dropMaxZIndex = 2;
                        break;
                    case 3:
                        hue = 0x55;
                        if (area < 3)
                            continue;
                        x += 3;
                        y -= 3;
                        break;
                    case 4:
                        hue = 0x55;
                        if (area < 3)
                            continue;
                        x -= 2;
                        y += 3;
                        break;
                    case 5:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        x--;
                        y += 2;
                        break;
                    case 6://mandatory
                        y++;
                        break;
                    case 7://mandatory
                        x++;
                        break;
                    case 8:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        x += 2;
                        y--;
                        //dropMaxZIndex = 8;
                        break;
                    case 9:
                        hue = 0x55;
                        if (area < 3)
                            continue;
                        x += 3;
                        y -= 2;
                        break;
                    case 10:
                        hue = 0x75;
                        if (area < 4)
                            continue;
                        x += 4;
                        y -= 3;
                        break;
                    case 11:
                        hue = 0x55;
                        if (area < 3)
                            continue;
                        x--;
                        y += 3;
                        break;
                    case 12:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        y += 2;
                        break;
                    case 13://mandatory
                        x++;
                        y++;
                        break;
                    case 14:
                        hue = 0x45;
                        if (area < 2)
                            continue;
                        x += 2;
                        break;
                    case 15:
                        hue = 0x55;
                        if (area < 3)
                            continue;
                        x += 3;
                        y--;
                        break;
                    case 16:
                        hue = 0x75;
                        if (area < 4)
                            continue;
                        x += 4;
                        y -= 2;
                        break;
                    case 17:
                        hue = 0x75;
                        if (area < 4)
                            return;
                        y += 3;
                        break;
                    case 18:
                        hue = 0x75;
                        x++;
                        y += 2;
                        break;
                    case 19:
                        hue = 0x75;
                        x += 2;
                        y++;
                        break;
                    case 20:
                        hue = 0x75;
                        x += 3;
                        break;
                    case 21:
                        hue = 0x75;
                        x += 4;
                        y--;
                        break;
                }

                if (x < _minTile.X || x > _maxTile.X)
                    continue;

                if (y < _minTile.Y || y > _maxTile.Y)
                    continue;

                int currentMaxZ = maxZ;

                if (i == dropMaxZIndex)
                    currentMaxZ += 20;

                var tile = World.Map.GetTile(x, y);

                if (tile != null)
                {
                    if (AddTileToRenderList(tile.FirstNode, x, y, useObjectHandles, currentMaxZ, hue, i) == i)
                        break;
                }
            }
        }

        private void GetViewPort()
        {
            int oldDrawOffsetX = _offset.X;
            int oldDrawOffsetY = _offset.Y;
            int winGamePosX = 0;
            int winGamePosY = 0;
            int winGameWidth = Engine.Profile.Current.GameWindowSize.X;
            int winGameHeight = Engine.Profile.Current.GameWindowSize.Y;
            int winGameCenterX = winGamePosX + (winGameWidth >> 1);
            int winGameCenterY = winGamePosY + (winGameHeight >> 1) + (World.Player.Z << 2);
            winGameCenterX -= (int) World.Player.Offset.X;
            winGameCenterY -= (int) (World.Player.Offset.Y - World.Player.Offset.Z);
            int winDrawOffsetX = (World.Player.X - World.Player.Y) * 22 - winGameCenterX;
            int winDrawOffsetY = (World.Player.X + World.Player.Y) * 22 - winGameCenterY;

            int winGameScaledOffsetX;
            int winGameScaledOffsetY;
            int winGameScaledWidth;
            int winGameScaledHeight;

            if (Engine.Profile.Current != null && Engine.Profile.Current.EnableScaleZoom)
            {
                float left = winGamePosX;
                float right = winGameWidth + left;
                float top = winGamePosY;
                float bottom = winGameHeight + top;
                float newRight = right * Scale;
                float newBottom = bottom * Scale;

                winGameScaledOffsetX = (int)(left * Scale - (newRight - right));
                winGameScaledOffsetY = (int)(top * Scale - (newBottom - bottom));
                winGameScaledWidth = (int)(newRight - winGameScaledOffsetX);
                winGameScaledHeight = (int)(newBottom - winGameScaledOffsetY);
            }
            else
            {
                winGameScaledOffsetX = 0;
                winGameScaledOffsetY = 0;
                winGameScaledWidth = 0;
                winGameScaledHeight = 0;
            }


            int width = (int) ((winGameWidth / 44 + 1) * Scale);
            int height = (int) ((winGameHeight / 44 + 1) * Scale);

            winDrawOffsetX += winGameScaledOffsetX >> 1;
            winDrawOffsetY += winGameScaledOffsetY >> 1;

            const int MAX = 70;

            if (width > MAX)
                width = MAX;

            if (height > MAX)
                height = MAX;

            int size = Math.Max(width, height);

            if (size < World.ClientViewRange)
                size = World.ClientViewRange;

            int realMinRangeX = World.Player.X - size;

            if (realMinRangeX < 0)
                realMinRangeX = 0;
            int realMaxRangeX = World.Player.X + size;

            //if (realMaxRangeX >= FileManager.Map.MapsDefaultSize[World.Map.Index][0])
            //    realMaxRangeX = FileManager.Map.MapsDefaultSize[World.Map.Index][0];
            int realMinRangeY = World.Player.Y - size;

            if (realMinRangeY < 0)
                realMinRangeY = 0;
            int realMaxRangeY = World.Player.Y + size;

            //if (realMaxRangeY >= FileManager.Map.MapsDefaultSize[World.Map.Index][1])
            //    realMaxRangeY = FileManager.Map.MapsDefaultSize[World.Map.Index][1];
            int minBlockX = (realMinRangeX >> 3) - 1;
            int minBlockY = (realMinRangeY >> 3) - 1;
            int maxBlockX = (realMaxRangeX >> 3) + 1;
            int maxBlockY = (realMaxRangeY >> 3) + 1;

            if (minBlockX < 0)
                minBlockX = 0;

            if (minBlockY < 0)
                minBlockY = 0;

            if (maxBlockX >= FileManager.Map.MapsDefaultSize[World.Map.Index, 0])
                maxBlockX = FileManager.Map.MapsDefaultSize[World.Map.Index, 0] - 1;

            if (maxBlockY >= FileManager.Map.MapsDefaultSize[World.Map.Index, 1])
                maxBlockY = FileManager.Map.MapsDefaultSize[World.Map.Index, 1] - 1;

            int drawOffset = (int) (Scale * 40.0);
            float maxX = winGamePosX + winGameWidth + drawOffset;
            float maxY = winGamePosY + winGameHeight + drawOffset;
            float newMaxX = maxX * Scale;
            float newMaxY = maxY * Scale;
            int minPixelsX = (int) ((winGamePosX - drawOffset) * Scale - MAX /*- (newMaxX + maxX)*/);
            int maxPixelsX = (int) newMaxX;
            int minPixelsY = (int) ((winGamePosY - drawOffset) * Scale - MAX /*- (newMaxY + maxY)*/);
            int maxPixlesY = (int) newMaxY;

            if (UpdateDrawPosition || oldDrawOffsetX != winDrawOffsetX || oldDrawOffsetY != winDrawOffsetY)
                UpdateDrawPosition = true;

            _minTile.X = realMinRangeX;
            _minTile.Y = realMinRangeY;
            _maxTile.X = realMaxRangeX;
            _maxTile.Y = realMaxRangeY;

            _minPixel.X = minPixelsX;
            _minPixel.Y = minPixelsY;
            _maxPixel.X = maxPixelsX;
            _maxPixel.Y = maxPixlesY;

            _offset.X = winDrawOffsetX;
            _offset.Y = winDrawOffsetY;


            UpdateMaxDrawZ();
        }
    }
}