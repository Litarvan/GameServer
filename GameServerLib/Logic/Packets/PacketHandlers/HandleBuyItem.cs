﻿using ENet;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.Packets.PacketDefinitions.C2S;
using LeagueSandbox.GameServer.Logic.Players;

namespace LeagueSandbox.GameServer.Logic.Packets.PacketHandlers
{
    public class HandleBuyItem : PacketHandlerBase
    {
        private readonly Game _game;
        private readonly ItemManager _itemManager;
        private readonly PlayerManager _playerManager;

        public override PacketCmd PacketType => PacketCmd.PKT_C2S_BuyItemReq;
        public override Channel PacketChannel => Channel.CHL_C2S;

        public HandleBuyItem(Game game, ItemManager itemManager, PlayerManager playerManager)
        {
            _game = game;
            _itemManager = itemManager;
            _playerManager = playerManager;
        }

        public override bool HandlePacket(Peer peer, byte[] data)
        {
            var request = new BuyItemRequest(data);

            var itemTemplate = _itemManager.SafeGetItemType(request.id);
            if (itemTemplate == null)
                return false;

            var champion = _playerManager.GetPeerInfo(peer).Champion;
            var stats = champion.GetStats();
            var inventory = champion.getInventory();
            var recipeParts = inventory.GetAvailableItems(itemTemplate.Recipe);
            var price = itemTemplate.TotalPrice;
            Item i;
            short itemSlot = 0;

            if (recipeParts.Count == 0)
            {
                if (stats.Gold < price)
                {
                    return true;
                }

                i = inventory.AddItem(itemTemplate);

                if (i == null)
                { // Slots full
                    return false;
                }
                else
                {
                    itemSlot = inventory.GetItemSlot(i);
                }
            }
            else
            {
                foreach (var instance in recipeParts)
                    price -= instance.ItemType.TotalPrice;

                if (stats.Gold < price)
                    return false;

                foreach (var instance in recipeParts)
                {
                    stats.RemoveModifier(instance.ItemType);
                    _game.PacketNotifier.NotifyRemoveItem(champion, inventory.GetItemSlot(instance), 0);
                    inventory.RemoveItem(instance);
                }

                i = inventory.AddItem(itemTemplate);
                itemSlot = inventory.GetItemSlot(i);
            }

            short spellSlot = (short)(itemSlot + 6);

            champion.Spells[spellSlot] = new Spell(champion,i.ItemType.SpellName, (byte)spellSlot);

            stats.Gold -= price;
            stats.AddModifier(itemTemplate);
            _game.PacketNotifier.NotifyItemBought(champion, i);

            return true;
        }
    }
}
